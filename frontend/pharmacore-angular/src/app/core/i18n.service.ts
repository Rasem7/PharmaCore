import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';

export type Lang = 'ar' | 'en';
const STORAGE_KEY = 'pc.lang';

@Injectable({ providedIn: 'root' })
export class I18nService {
  private http = inject(HttpClient);
  readonly lang = signal<Lang>('ar');
  readonly dict = signal<Record<string, string>>({});

  async init(): Promise<void> {
    let saved: string | null = null;
    try { saved = localStorage.getItem(STORAGE_KEY); } catch { /* storage may be unavailable */ }
    await this.use(saved === 'en' ? 'en' : 'ar');
  }

  async use(lang: Lang): Promise<void> {
    const dict = await firstValueFrom(this.http.get<Record<string, string>>(`assets/i18n/${lang}.json`));
    this.dict.set(dict);
    this.lang.set(lang);
    const root = document.documentElement;
    root.lang = lang;
    root.dir = lang === 'ar' ? 'rtl' : 'ltr';
    try { localStorage.setItem(STORAGE_KEY, lang); } catch { /* ignore */ }
  }

  toggle(): Promise<void> { return this.use(this.lang() === 'ar' ? 'en' : 'ar'); }

  t(key: string): string { return this.dict()[key] ?? key; }
}
