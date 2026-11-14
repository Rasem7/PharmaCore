import { HttpClient } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { API_BASE_URL } from '../../core/api-config';
import { TranslatePipe } from '../../core/translate.pipe';

@Component({
  selector: 'pc-dashboard',
  imports: [TranslatePipe],
  template: `
    <h1 class="page-title">{{ 'dashboard.title' | t }}</h1>
    <section class="card">
      <div class="row">
        <strong>{{ 'dashboard.api' | t }}</strong>
        @switch (api()) {
          @case ('ok') { <span class="badge ok">{{ 'dashboard.api.ok' | t }}</span> }
          @case ('down') { <span class="badge danger">{{ 'dashboard.api.down' | t }}</span> }
          @default { <span class="badge">{{ 'dashboard.api.checking' | t }}</span> }
        }
      </div>
      <p class="note">{{ 'dashboard.note' | t }}</p>
    </section>
  `,
  styles: [`.note { color: var(--pc-muted); margin: 14px 0 0; }`]
})
export class Dashboard {
  api = signal<'checking' | 'ok' | 'down'>('checking');

  constructor() {
    // /health returns the health-check status text ("Healthy") and includes the SQL Server probe.
    inject(HttpClient).get(`${API_BASE_URL}/health`, { responseType: 'text' }).subscribe({
      next: () => this.api.set('ok'),
      error: () => this.api.set('down')
    });
  }
}
