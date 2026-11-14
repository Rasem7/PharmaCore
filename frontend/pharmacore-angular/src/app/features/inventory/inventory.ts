import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { AbstractControl, FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { AuthService } from '../../core/auth.service';
import { TranslatePipe } from '../../core/translate.pipe';
import { Product, ProductsService } from '../products/products.service';
import { InventoryService, StockRow, Warehouse } from './inventory.service';

type Panel = 'none' | 'receive' | 'adjust';

@Component({
  selector: 'pc-inventory',
  imports: [ReactiveFormsModule, TranslatePipe],
  templateUrl: './inventory.html',
  styleUrl: './inventory.scss'
})
export class Inventory {
  private api = inject(InventoryService);
  private products = inject(ProductsService);
  private fb = inject(FormBuilder);
  auth = inject(AuthService);

  warehouses = signal<Warehouse[]>([]);
  warehouseId = signal<string | null>(null);
  rows = signal<StockRow[]>([]);
  loading = signal(false);
  filter = signal('');
  panel = signal<Panel>('none');
  error = signal<string | null>(null);
  saving = signal(false);
  notice = signal<string | null>(null);

  shown = computed(() => {
    const f = this.filter().trim().toLowerCase();
    return f ? this.rows().filter(r => r.productName.toLowerCase().includes(f) || r.batchNumber.toLowerCase().includes(f)) : this.rows();
  });

  // Receive form
  productQuery = signal('');
  productResults = signal<Product[]>([]);
  product = signal<Product | null>(null);
  private searchTimer: ReturnType<typeof setTimeout> | undefined;
  private receiveKey = crypto.randomUUID(); // reused on retry so a double click or a retry cannot receive twice
  receiveForm = this.fb.nonNullable.group({
    batchNumber: ['', [Validators.required, Validators.maxLength(60)]],
    expiryDate: ['', Validators.required],
    productionDate: [''],
    quantity: [1, [Validators.required, Validators.min(0.001)]]
  });

  // Adjust form
  adjusting = signal<StockRow | null>(null);
  private adjustKey = crypto.randomUUID();
  adjustForm = this.fb.nonNullable.group({
    quantityDelta: [0, Validators.required],
    reason: ['', [Validators.required, Validators.maxLength(300)]]
  });

  constructor() {
    this.api.warehouses().subscribe(w => {
      this.warehouses.set(w);
      this.warehouseId.set((w.find(x => x.isDefault) ?? w[0])?.id ?? null);
      this.load();
    });
  }

  load(): void {
    this.loading.set(true);
    this.api.stock(this.warehouseId()).subscribe({
      next: r => { this.rows.set(r); this.loading.set(false); },
      error: () => this.loading.set(false)
    });
  }

  selectWarehouse(id: string): void { this.warehouseId.set(id); this.load(); }

  /** Display-only status. Real expiry rules (FEFO, never selling expired stock) are enforced by the backend. */
  expiryStatus(date: string): 'expired' | 'soon' | 'ok' {
    const today = new Date().toISOString().slice(0, 10);
    if (date < today) return 'expired';
    const limit = new Date(); limit.setDate(limit.getDate() + 90);
    return date <= limit.toISOString().slice(0, 10) ? 'soon' : 'ok';
  }

  // ----- receive -----
  openReceive(): void {
    this.error.set(null); this.notice.set(null); this.product.set(null); this.productQuery.set(''); this.productResults.set([]);
    this.receiveForm.reset({ batchNumber: '', expiryDate: '', productionDate: '', quantity: 1 });
    this.panel.set('receive');
  }

  onProductSearch(value: string): void {
    this.productQuery.set(value); this.product.set(null);
    clearTimeout(this.searchTimer);
    if (value.trim().length < 2) { this.productResults.set([]); return; }
    this.searchTimer = setTimeout(() =>
      this.products.search(value.trim(), 1, 8).subscribe(r => this.productResults.set(r.items)), 250);
  }

  pickProduct(p: Product): void { this.product.set(p); this.productQuery.set(p.commercialName); this.productResults.set([]); }

  submitReceive(): void {
    this.receiveForm.markAllAsTouched();
    const v = this.receiveForm.getRawValue();
    const wh = this.warehouseId(); const p = this.product();
    if (!wh || !p || this.receiveForm.invalid) { if (!p) this.error.set('inventory.err.pickProduct'); return; }
    if (v.productionDate && v.productionDate >= v.expiryDate) { this.error.set('inventory.err.dates'); return; }
    this.saving.set(true); this.error.set(null);
    this.api.receive({ warehouseId: wh, productId: p.id, batchNumber: v.batchNumber.trim(), productionDate: v.productionDate || null,
      expiryDate: v.expiryDate, quantity: Number(v.quantity), idempotencyKey: this.receiveKey }).subscribe({
      next: () => { this.saving.set(false); this.receiveKey = crypto.randomUUID(); this.panel.set('none'); this.notice.set('inventory.received'); this.load(); },
      error: (e: HttpErrorResponse) => { this.saving.set(false); this.error.set(this.mapError(e)); }
    });
  }

  // ----- adjust -----
  openAdjust(r: StockRow): void {
    this.error.set(null); this.notice.set(null); this.adjusting.set(r);
    this.adjustForm.reset({ quantityDelta: 0, reason: '' });
    this.panel.set('adjust');
  }

  submitAdjust(): void {
    this.adjustForm.markAllAsTouched();
    const r = this.adjusting();
    const v = this.adjustForm.getRawValue();
    if (!r || this.adjustForm.invalid || Number(v.quantityDelta) === 0) { this.error.set('inventory.err.delta'); return; }
    this.saving.set(true); this.error.set(null);
    this.api.adjust({ warehouseId: r.warehouseId, batchId: r.batchId, quantityDelta: Number(v.quantityDelta),
      reason: v.reason.trim(), idempotencyKey: this.adjustKey }).subscribe({
      next: () => { this.saving.set(false); this.adjustKey = crypto.randomUUID(); this.panel.set('none'); this.notice.set('inventory.adjusted'); this.load(); },
      error: (e: HttpErrorResponse) => { this.saving.set(false); this.error.set(this.mapError(e)); }
    });
  }

  close(): void { this.panel.set('none'); this.error.set(null); }

  invalid(form: 'receive' | 'adjust', name: string): boolean {
    const group: AbstractControl = form === 'receive' ? this.receiveForm : this.adjustForm;
    const c = group.get(name);
    return !!c && c.invalid && (c.touched || c.dirty);
  }

  private mapError(e: HttpErrorResponse): string {
    if (e.status === 409) return 'inventory.err.negative';
    if (e.status === 404) return 'inventory.err.notFound';
    if (e.status === 403) return 'inventory.err.forbidden';
    return 'common.saveFailed';
  }
}
