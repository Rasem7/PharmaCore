import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { API_BASE_URL } from '../../core/api-config';

export interface DrugRef {
  id: string; nameEn: string; nameAr: string | null; scientificName: string | null; manufacturer: string | null;
  drugClass: string | null; route: string | null; strengthText: string | null; referencePrice: number; isDrug: boolean;
}
export interface DrugPage { items: DrugRef[]; total: number; page: number; pageSize: number; }
export interface ImportIssue { rowNumber: number; message: string; rowRejected: boolean; }
export interface ImportReport {
  batchId: string; fileName: string; totalRows: number; validRows: number; inserted: number; updated: number;
  unchanged: number; duplicatesInFile: number; rejected: number; issues: ImportIssue[];
}

@Injectable({ providedIn: 'root' })
export class DrugsService {
  private http = inject(HttpClient);
  private base = `${API_BASE_URL}/api/drug-reference`;

  search(search: string, drugsOnly: boolean, page: number, pageSize: number) {
    let params = new HttpParams().set('drugsOnly', drugsOnly).set('page', page).set('pageSize', pageSize);
    if (search) params = params.set('search', search);
    return this.http.get<DrugPage>(this.base, { params });
  }
  alternatives(id: string) { return this.http.get<DrugRef[]>(`${this.base}/${id}/alternatives`); }
  import(file: File) {
    const form = new FormData();
    form.append('file', file);
    return this.http.post<ImportReport>(`${this.base}/import`, form);
  }
}
