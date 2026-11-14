import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { tap } from 'rxjs';
import { API_BASE_URL } from './api-config';

export interface LoginResponse { accessToken: string; userName: string; permissions: string[]; }
const KEY = 'pc.session';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private http = inject(HttpClient);
  private router = inject(Router);
  private session = signal<LoginResponse | null>(AuthService.restore());

  readonly isAuthenticated = computed(() => this.session() !== null);
  readonly userName = computed(() => this.session()?.userName ?? '');
  readonly token = computed(() => this.session()?.accessToken ?? null);

  /** UI convenience only. The API enforces permissions on every request. */
  can(permission: string): boolean { return this.session()?.permissions.includes(permission) ?? false; }

  login(userName: string, password: string) {
    return this.http.post<LoginResponse>(`${API_BASE_URL}/api/auth/login`, { userName, password })
      .pipe(tap(r => { this.session.set(r); try { sessionStorage.setItem(KEY, JSON.stringify(r)); } catch { /* ignore */ } }));
  }

  logout(): void {
    this.session.set(null);
    try { sessionStorage.removeItem(KEY); } catch { /* ignore */ }
    this.router.navigate(['/login']);
  }

  private static restore(): LoginResponse | null {
    try { const raw = sessionStorage.getItem(KEY); return raw ? JSON.parse(raw) as LoginResponse : null; } catch { return null; }
  }
}
