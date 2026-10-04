import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map } from 'rxjs';
import {
  Asset, AssetMovement, AssetQuery, CreateAssetRequest, Employee, PagedResult, Supplier,
} from './models';

/** La API guarda fechas con hora en UTC pero las serializa sin 'Z'; se la agregamos para mostrarlas en hora local. */
const asUtc = (s: string): string => (/[zZ]|[+-]\d\d:\d\d$/.test(s) ? s : s + 'Z');

@Injectable({ providedIn: 'root' })
export class ApiService {
  private readonly http = inject(HttpClient);

  // ---------- Activos ----------
  getAssets(q: AssetQuery) {
    let params = new HttpParams().set('page', q.page).set('pageSize', q.pageSize);
    if (q.search?.trim()) params = params.set('search', q.search.trim());
    if (q.status) params = params.set('status', q.status);
    if (q.category) params = params.set('category', q.category);
    return this.http.get<PagedResult<Asset>>('/api/assets', { params }).pipe(
      map((r) => ({ ...r, items: r.items.map((a) => this.fixAsset(a)) })),
    );
  }

  getAsset(id: number) {
    return this.http.get<Asset>(`/api/assets/${id}`).pipe(map((a) => this.fixAsset(a)));
  }

  createAsset(body: CreateAssetRequest) {
    return this.http.post<Asset>('/api/assets', body);
  }

  assign(assetId: number, body: { employeeId: number; notes: string | null }) {
    return this.http.post<{ assignmentId: number }>(`/api/assets/${assetId}/assign`, body);
  }

  returnAsset(assetId: number, body: { returnCondition: string; notes: string | null }) {
    return this.http.post<{ assignmentId: number }>(`/api/assets/${assetId}/return`, body);
  }

  getHistory(assetId: number, page: number, pageSize: number) {
    const params = new HttpParams().set('page', page).set('pageSize', pageSize);
    return this.http.get<PagedResult<AssetMovement>>(`/api/assets/${assetId}/history`, { params }).pipe(
      map((r) => ({ ...r, items: r.items.map((m) => ({ ...m, performedAt: asUtc(m.performedAt) })) })),
    );
  }

  // ---------- Catálogos ----------
  getActiveEmployees() {
    const params = new HttpParams().set('isActive', true).set('page', 1).set('pageSize', 100);
    return this.http.get<PagedResult<Employee>>('/api/employees', { params });
  }

  getSuppliers() {
    const params = new HttpParams().set('page', 1).set('pageSize', 100);
    return this.http.get<PagedResult<Supplier>>('/api/suppliers', { params });
  }

  private fixAsset(a: Asset): Asset {
    return { ...a, createdAt: asUtc(a.createdAt), updatedAt: asUtc(a.updatedAt) };
  }
}
