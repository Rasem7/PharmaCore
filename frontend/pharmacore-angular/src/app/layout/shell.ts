import { Component, inject } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../core/auth.service';
import { I18nService } from '../core/i18n.service';
import { TranslatePipe } from '../core/translate.pipe';

@Component({
  selector: 'pc-shell',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, TranslatePipe],
  template: `
    <aside class="sidebar">
      <div class="brand">{{ 'app.name' | t }}</div>
      <nav>
        <a routerLink="/dashboard" routerLinkActive="active">{{ 'nav.dashboard' | t }}</a>
        @if (auth.can('products.read')) {
          <a routerLink="/products" routerLinkActive="active">{{ 'nav.products' | t }}</a>
        }
        @if (auth.can('inventory.read')) {
          <a routerLink="/inventory" routerLinkActive="active">{{ 'nav.inventory' | t }}</a>
        }
        @if (auth.can('drugref.read')) {
          <a routerLink="/drugs" routerLinkActive="active">{{ 'nav.drugs' | t }}</a>
        }
      </nav>
    </aside>
    <div class="main">
      <header class="topbar">
        <span class="spacer"></span>
        <button class="btn" type="button" (click)="i18n.toggle()">{{ 'lang.switch' | t }}</button>
        <span class="user">{{ auth.userName() }}</span>
        <button class="btn" type="button" (click)="auth.logout()">{{ 'nav.logout' | t }}</button>
      </header>
      <main class="content"><router-outlet /></main>
    </div>
  `,
  styles: [`
    :host { display: flex; min-height: 100vh; }
    .sidebar { width: 230px; background: var(--pc-navy); color: #dbe6f7; padding: 16px 0; flex-shrink: 0; }
    .brand { font-size: 20px; font-weight: 700; padding: 0 20px 20px; color: #fff; }
    nav a { display: block; padding: 11px 20px; color: inherit; text-decoration: none; border-inline-start: 3px solid transparent; }
    nav a:hover { background: var(--pc-navy-2); }
    nav a.active { background: var(--pc-navy-2); border-inline-start-color: #6ea1ff; color: #fff; }
    .main { flex: 1; display: flex; flex-direction: column; min-width: 0; }
    .topbar { display: flex; align-items: center; gap: 12px; padding: 10px 24px; background: #fff; border-bottom: 1px solid var(--pc-border); }
    .user { color: var(--pc-muted); }
    .content { padding: 24px; }
  `]
})
export class Shell {
  auth = inject(AuthService);
  i18n = inject(I18nService);
}
