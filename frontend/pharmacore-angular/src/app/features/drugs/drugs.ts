import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { AuthService } from '../../core/auth.service';
import { TranslatePipe } from '../../core/translate.pipe';
import { DrugRef, DrugsService, ImportReport } from './drugs.service';

const PAGE_SIZE = 20;

@Component({
  selector: 'pc-drugs',
  imports: [TranslatePipe],
  templateUrl: './drugs.html',
  styleUrl: './drugs.scss'
})
export class Drugs {
  private api = inject(DrugsService);
  private router = inject(Router);
  auth = inject(AuthService);

  items = signal<DrugRef[]>([]);
  total = signal(0);
  page = signal(1);
  search = signal('');
  drugsOnly = signal(true);
  loading = signal(false);
  pages = computed(() => Math.max(1, Math.ceil(this.total() / PAGE_SIZE)));

  selected = signal<DrugRef | null>(null);
  alternatives = signal<DrugRef[]>([]);
  altLoading = signal(false);

  importing = signal(false);
  report = signal<ImportReport | null>(null);
  importError = signal<string | null>(null);

  private timer: ReturnType<typeof setTimeout> | undefined;

  constructor() { this.load(); }

  load(): void {
    this.loading.set(true);
    this.api.search(this.search(), this.drugsOnly(), this.page(), PAGE_SIZE).subscribe({
      next: r => { this.items.set(r.items); this.total.set(r.total); this.loading.set(false); },
      error: () => this.loading.set(false)
    });
  }

  onSearch(value: string): void {
    this.search.set(value);
    clearTimeout(this.timer);
    this.timer = setTimeout(() => { this.page.set(1); this.load(); }, 300);
  }

  toggleDrugsOnly(checked: boolean): void { this.drugsOnly.set(checked); this.page.set(1); this.load(); }

  go(delta: number): void {
    const next = this.page() + delta;
    if (next < 1 || next > this.pages()) return;
    this.page.set(next); this.load();
  }

  showAlternatives(d: DrugRef): void {
    this.selected.set(d); this.alternatives.set([]); this.altLoading.set(true);
    this.api.alternatives(d.id).subscribe({
      next: a => { this.alternatives.set(a); this.altLoading.set(false); },
      error: () => this.altLoading.set(false)
    });
  }

  /** Hands the reference to the Products form; the user still reviews and saves it there. */
  addToProducts(d: DrugRef): void { this.router.navigate(['/products'], { state: { prefill: d } }); }

  onFile(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;
    this.importing.set(true); this.report.set(null); this.importError.set(null);
    this.api.import(file).subscribe({
      next: r => { this.report.set(r); this.importing.set(false); input.value = ''; this.load(); },
      error: (e: HttpErrorResponse) => {
        this.importing.set(false); input.value = '';
        this.importError.set(e.error?.title ?? 'drugs.import.failed');
      }
    });
  }
}
