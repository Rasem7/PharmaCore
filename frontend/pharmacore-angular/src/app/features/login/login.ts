import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../core/auth.service';
import { I18nService } from '../../core/i18n.service';
import { TranslatePipe } from '../../core/translate.pipe';

@Component({
  selector: 'pc-login',
  imports: [ReactiveFormsModule, TranslatePipe],
  template: `
    <div class="wrap">
      <form class="card" [formGroup]="form" (ngSubmit)="submit()">
        <h1>{{ 'app.name' | t }}</h1>
        <h2>{{ 'login.title' | t }}</h2>
        <div class="field">
          <label for="u">{{ 'login.username' | t }}</label>
          <input id="u" formControlName="userName" autocomplete="username" autofocus>
        </div>
        <div class="field">
          <label for="p">{{ 'login.password' | t }}</label>
          <input id="p" type="password" formControlName="password" autocomplete="current-password">
        </div>
        @if (error()) { <p class="error" role="alert">{{ error()! | t }}</p> }
        <button class="btn primary" type="submit" [disabled]="form.invalid || busy()">{{ 'login.submit' | t }}</button>
        <button class="btn link" type="button" (click)="i18n.toggle()">{{ 'lang.switch' | t }}</button>
      </form>
    </div>
  `,
  styles: [`
    .wrap { min-height: 100vh; display: grid; place-items: center; }
    form { width: 360px; display: flex; flex-direction: column; }
    h1 { margin: 0; color: var(--pc-navy); } h2 { margin: 4px 0 18px; font-weight: 500; font-size: 16px; color: var(--pc-muted); }
    .link { margin-top: 10px; border: none; background: none; color: var(--pc-accent); }
  `]
})
export class Login {
  private fb = inject(FormBuilder);
  private auth = inject(AuthService);
  private router = inject(Router);
  i18n = inject(I18nService);

  busy = signal(false);
  error = signal<string | null>(null);
  form = this.fb.nonNullable.group({ userName: ['', Validators.required], password: ['', Validators.required] });

  submit(): void {
    if (this.form.invalid) return;
    this.busy.set(true); this.error.set(null);
    const { userName, password } = this.form.getRawValue();
    this.auth.login(userName, password).subscribe({
      next: () => this.router.navigate(['/dashboard']),
      error: (e: HttpErrorResponse) => { this.busy.set(false); this.error.set(e.status === 429 ? 'login.rateLimited' : 'login.failed'); }
    });
  }
}
