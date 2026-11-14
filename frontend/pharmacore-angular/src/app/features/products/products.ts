import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { DrugRef } from '../drugs/drugs.service';
import { AuthService } from '../../core/auth.service';
import { TranslatePipe } from '../../core/translate.pipe';
import { Product, ProductsService, SaveProduct } from './products.service';

const PAGE_SIZE = 20;

@Component({
  selector: 'pc-products',
  imports: [ReactiveFormsModule, TranslatePipe],
  templateUrl: './products.html',
  styleUrl: './products.scss'
})
export class Products {
  private api = inject(ProductsService);
  private fb = inject(FormBuilder);
  auth = inject(AuthService);

  items = signal<Product[]>([]);
  total = signal(0);
  page = signal(1);
  search = signal('');
  loading = signal(false);
  editing = signal<Product | 'new' | null>(null);
  saveError = signal<string | null>(null);
  saving = signal(false);
  pages = computed(() => Math.max(1, Math.ceil(this.total() / PAGE_SIZE)));

  private timer: ReturnType<typeof setTimeout> | undefined;

  form = this.fb.nonNullable.group({
    commercialName: ['', [Validators.required, Validators.maxLength(200)]],
    nameAr: [''], nameEn: [''], scientificName: [''],
    primaryBarcode: ['', Validators.maxLength(64)],
    pharmaceuticalForm: [''],
    purchasePrice: [0, [Validators.required, Validators.min(0)]],
    sellingPrice: [0, [Validators.required, Validators.min(0)]],
    minStock: [0, [Validators.required, Validators.min(0)]],
    maxStock: [null as number | null, Validators.min(0)],
    status: [1 as 1 | 2 | 3]
  });

  constructor() {
    this.load();
    // Arrived from the drug reference page: open a new-product form pre-filled with catalogue data.
    // Purchase price is NOT guessed; the pharmacist enters it. The reference price is only a starting selling price.
    const pre = inject(Router).getCurrentNavigation()?.extras.state?.['prefill'] as DrugRef | undefined;
    if (pre) {
      this.openNew();
      this.form.patchValue({
        commercialName: pre.nameEn, nameEn: pre.nameEn, nameAr: pre.nameAr ?? '',
        scientificName: pre.scientificName ?? '', sellingPrice: pre.referencePrice
      });
    }
  }

  load(): void {
    this.loading.set(true);
    this.api.search(this.search(), this.page(), PAGE_SIZE).subscribe({
      next: r => { this.items.set(r.items); this.total.set(r.total); this.loading.set(false); },
      error: () => this.loading.set(false)
    });
  }

  onSearch(value: string): void {
    this.search.set(value);
    clearTimeout(this.timer);
    this.timer = setTimeout(() => { this.page.set(1); this.load(); }, 300);
  }

  go(delta: number): void {
    const next = this.page() + delta;
    if (next < 1 || next > this.pages()) return;
    this.page.set(next); this.load();
  }

  openNew(): void {
    this.form.reset({ purchasePrice: 0, sellingPrice: 0, minStock: 0, maxStock: null, status: 1 });
    this.saveError.set(null); this.editing.set('new');
  }

  openEdit(p: Product): void {
    this.form.reset({ ...p, nameAr: p.nameAr ?? '', nameEn: p.nameEn ?? '', scientificName: p.scientificName ?? '',
      primaryBarcode: p.primaryBarcode ?? '', pharmaceuticalForm: p.pharmaceuticalForm ?? '' });
    this.saveError.set(null); this.editing.set(p);
  }

  cancel(): void { this.editing.set(null); }

  invalid(name: string): boolean {
    const c = this.form.get(name);
    return !!c && c.invalid && (c.touched || c.dirty);
  }

  save(): void {
    this.form.markAllAsTouched();
    if (this.form.invalid) return;
    const v = this.form.getRawValue();
    const body: SaveProduct = {
      ...v,
      nameAr: v.nameAr || null, nameEn: v.nameEn || null, scientificName: v.scientificName || null,
      primaryBarcode: v.primaryBarcode || null, pharmaceuticalForm: v.pharmaceuticalForm || null,
      maxStock: v.maxStock === null || (v.maxStock as unknown) === '' ? null : Number(v.maxStock),
      status: Number(v.status) as 1 | 2 | 3
    };
    const target = this.editing();
    const req = target && target !== 'new' ? this.api.update(target.id, body) : this.api.create(body);
    this.saving.set(true); this.saveError.set(null);
    req.subscribe({
      next: () => { this.saving.set(false); this.editing.set(null); this.load(); },
      error: (e: HttpErrorResponse) => {
        this.saving.set(false);
        this.saveError.set(e.status === 409 ? 'common.duplicateBarcode' : 'common.saveFailed');
      }
    });
  }

  statusClass(s: number): string { return s === 1 ? 'ok' : s === 2 ? 'warn' : 'danger'; }
}
