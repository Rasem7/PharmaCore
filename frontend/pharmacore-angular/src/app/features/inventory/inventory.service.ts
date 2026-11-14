import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { API_BASE_URL } from '../../core/api-config';

export interface Warehouse { id: string; branchName: string; name: string; isDefault: boolean; }
export interface StockRow {
  productId: string; productName: string; batchId: string; batchNumber: string;
  expiryDate: string; // yyyy-MM-dd
  warehouseId: string; quantity: number;
}
export interface ReceiveBody {
  warehouseId: string; productId: string; batchNumber: string; productionDate: string | null;
  expiryDate: string; quantity: number; idempotencyKey: string;
}
export interface AdjustBody { warehouseId: string; batchId: string; quantityDelta: number; reason: string; idempotencyKey: string; }

@Injectable({ providedIn: 'root' })
export class InventoryService {
  private http = inject(HttpClient);
  private base = `${API_BASE_URL}/api/inventory`;

  warehouses() { return this.http.get<Warehouse[]>(`${this.base}/warehouses`); }
  stock(warehouseId: string | null) {
    let params = new HttpParams();
    if (warehouseId) params = params.set('warehouseId', warehouseId);
    return this.http.get<StockRow[]>(`${this.base}/stock`, { params });
  }
  receive(b: ReceiveBody) { return this.http.post<void>(`${this.base}/receive`, b); }
  adjust(b: AdjustBody) { return this.http.post<void>(`${this.base}/adjust`, b); }
}
