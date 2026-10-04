import { DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../core/api.service';
import { AuthService } from '../../core/auth.service';
import { errorMessage } from '../../core/error.util';
import { ASSET_STATUSES, Asset, CATEGORIES, PagedResult } from '../../core/models';

@Component({
  selector: 'app-assets-list',
  imports: [FormsModule, RouterLink, DatePipe],
  template: `
    <div class="page-head">
      <h1>Activos</h1>
      @if (auth.isAdmin()) {
        <a routerLink="/assets/new" class="btn primary">+ Nuevo activo</a>
      }
    </div>

    <form class="card filters" (ngSubmit)="applyFilters()">
      <input name="search" [(ngModel)]="search" placeholder="Buscar: código, serie, marca, modelo…" maxlength="100" />
      <select name="status" [(ngModel)]="status">
        <option value="">Todos los estados</option>
        @for (s of statuses; track s) { <option [value]="s">{{ s }}</option> }
      </select>
      <select name="category" [(ngModel)]="category">
        <option value="">Todas las categorías</option>
        @for (c of categories; track c) { <option [value]="c">{{ c }}</option> }
      </select>
      <button class="btn primary" type="submit">Buscar</button>
      <button class="btn" type="button" (click)="reset()">Limpiar</button>
    </form>

    @if (error()) { <div class="alert error">{{ error() }}</div> }

    <div class="card table-wrap">
      <table>
        <thead>
          <tr>
            <th>Código</th><th>Categoría</th><th>Marca / Modelo</th><th>Serie</th>
            <th>Estado</th><th>Asignado a</th><th>Ubicación</th><th>Propiedad</th><th></th>
          </tr>
        </thead>
        <tbody>
          @for (a of result()?.items ?? []; track a.id) {
            <tr>
              <td><a [routerLink]="['/assets', a.id]"><strong>{{ a.assetCode }}</strong></a></td>
              <td>{{ a.category }}</td>
              <td>{{ a.brand }} {{ a.model }}</td>
              <td>{{ a.serialNumber ?? '—' }}</td>
              <td><span [class]="'badge ' + badgeClass(a)">{{ a.status }}</span></td>
              <td>{{ a.assignedEmployeeName ?? '—' }}</td>
              <td>{{ a.currentLocation ?? '—' }}</td>
              <td>{{ a.ownershipType }}@if (a.supplierName) { <span class="muted"> · {{ a.supplierName }}</span> }
                @if (a.rentalEndDate) { <div class="muted small">vence {{ a.rentalEndDate | date: 'dd/MM/yyyy' }}</div> }</td>
              <td><a [routerLink]="['/assets', a.id]" class="btn small">Ver</a></td>
            </tr>
          } @empty {
            <tr><td colspan="9" class="empty">{{ loading() ? 'Cargando…' : 'No se encontraron activos.' }}</td></tr>
          }
        </tbody>
      </table>
    </div>

    @if (result(); as r) {
      <div class="pager">
        <span class="muted">{{ r.totalCount }} resultado(s) · página {{ r.page }} de {{ r.totalPages || 1 }}</span>
        <div>
          <button class="btn small" (click)="goTo(r.page - 1)" [disabled]="r.page <= 1 || loading()">← Anterior</button>
          <button class="btn small" (click)="goTo(r.page + 1)" [disabled]="r.page >= r.totalPages || loading()">Siguiente →</button>
        </div>
      </div>
    }
  `,
})
export class AssetsListComponent implements OnInit {
  private readonly api = inject(ApiService);
  readonly auth = inject(AuthService);

  readonly statuses = ASSET_STATUSES;
  readonly categories = CATEGORIES;

  search = '';
  status = '';
  category = '';
  private readonly pageSize = 10;

  readonly result = signal<PagedResult<Asset> | null>(null);
  readonly loading = signal(false);
  readonly error = signal('');

  ngOnInit(): void {
    this.load(1);
  }

  applyFilters(): void { this.load(1); }

  reset(): void {
    this.search = this.status = this.category = '';
    this.load(1);
  }

  goTo(page: number): void { this.load(page); }

  badgeClass(a: Asset): string { return 'badge-' + a.status.toLowerCase(); }

  private load(page: number): void {
    this.loading.set(true);
    this.error.set('');
    this.api.getAssets({ search: this.search, status: this.status, category: this.category, page, pageSize: this.pageSize })
      .subscribe({
        next: (r) => { this.result.set(r); this.loading.set(false); },
        error: (err) => { this.error.set(errorMessage(err)); this.loading.set(false); },
      });
  }
}
