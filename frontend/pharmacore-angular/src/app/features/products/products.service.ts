import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { API_BASE_URL } from '../../core/api-config';

export interface Product {
  id: string;
  commercialName: string;
  nameAr: string | null;
  nameEn: string | null;
  scientificName: string | null;
  primaryBarcode: string | null;
  pharmaceuticalForm: string | null;
  purchasePrice: number;
  sellingPrice: number;
  minStock: number;
  maxStock: number | null;
  status: 1 | 2 | 3;
}
export type SaveProduct = Omit<Product, 'id'>;
export interface Paged<T> { items: T[]; total: number; page: number; pageSize: number; }

@Injectable({ providedIn: 'root' })
export class ProductsService {
  private http = inject(HttpClient);
  private base = `${API_BASE_URL}/api/products`;

  search(search: string, page: number, pageSize: number) {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (search) params = params.set('search', search);
    return this.http.get<Paged<Product>>(this.base, { params });
  }
  create(p: SaveProduct) { return this.http.post<Product>(this.base, p); }
  update(id: string, p: SaveProduct) { return this.http.put<Product>(`${this.base}/${id}`, p); }
}
