import { DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Observable } from 'rxjs';
import { ApiService } from '../../core/api.service';
import { errorMessage } from '../../core/error.util';
import { Asset, AssetMovement, Employee, PagedResult } from '../../core/models';

const MOVEMENT_LABELS: Record<string, string> = {
  Alta: 'Alta del activo',
  Asignacion: 'Asignación',
  Devolucion: 'Devolución',
  CambioEstado: 'Cambio de estado',
  CambioUbicacion: 'Cambio de ubicación',
};

@Component({
  selector: 'app-asset-detail',
  imports: [ReactiveFormsModule, RouterLink, DatePipe],
  template: `
    <div class="page-head">
      <h1>{{ asset()?.assetCode ?? 'Activo' }}</h1>
      <a routerLink="/assets" class="btn">← Volver</a>
    </div>

    @if (error()) { <div class="alert error">{{ error() }}</div> }
    @if (message()) { <div class="alert ok">{{ message() }}</div> }

    @if (asset(); as a) {
      <div class="card">
        <div class="detail-head">
          <h2>{{ a.brand }} {{ a.model }}</h2>
          <span [class]="'badge badge-' + a.status.toLowerCase()">{{ a.status }}</span>
        </div>
        <dl class="info-grid">
          <div><dt>Categoría</dt><dd>{{ a.category }}</dd></div>
          <div><dt>Número de serie</dt><dd>{{ a.serialNumber ?? '—' }}</dd></div>
          <div><dt>Propiedad</dt><dd>{{ a.ownershipType }}</dd></div>
          <div><dt>Proveedor</dt><dd>{{ a.supplierName ?? '—' }}</dd></div>
          <div><dt>Ubicación</dt><dd>{{ a.currentLocation ?? '—' }}</dd></div>
          <div><dt>Asignado a</dt><dd>{{ a.assignedEmployeeName ?? '—' }}</dd></div>
          <div><dt>Fecha de compra</dt><dd>{{ a.purchaseDate ? (a.purchaseDate | date: 'dd/MM/yyyy') : '—' }}</dd></div>
          <div><dt>Fin de renta</dt><dd>{{ a.rentalEndDate ? (a.rentalEndDate | date: 'dd/MM/yyyy') : '—' }}</dd></div>
          <div><dt>Creado</dt><dd>{{ a.createdAt | date: 'dd/MM/yyyy HH:mm' }}</dd></div>
          <div><dt>Actualizado</dt><dd>{{ a.updatedAt | date: 'dd/MM/yyyy HH:mm' }}</dd></div>
        </dl>
      </div>

      @if (a.status === 'Disponible') {
        <form class="card" [formGroup]="assignForm" (ngSubmit)="assign()">
          <h2>Asignar a un colaborador</h2>
          <div class="form-grid">
            <label>Colaborador *
              <select formControlName="employeeId">
                <option [ngValue]="null" disabled>Selecciona…</option>
                @for (e of employees(); track e.id) {
                  <option [ngValue]="e.id">{{ e.fullName }} ({{ e.employeeNumber }})</option>
                }
              </select>
            </label>
            <label>Notas
              <input formControlName="notes" maxlength="500" />
            </label>
          </div>
          <button class="btn primary" type="submit" [disabled]="assignForm.invalid || busy()">Asignar</button>
        </form>
      } @else if (a.status === 'Asignado') {
        <form class="card" [formGroup]="returnForm" (ngSubmit)="returnAsset()">
          <h2>Registrar devolución</h2>
          <div class="form-grid">
            <label>Condición del equipo *
              <input formControlName="returnCondition" maxlength="200" placeholder="Ej. Buen estado, con rayones leves…" />
            </label>
            <label>Notas
              <input formControlName="notes" maxlength="500" />
            </label>
          </div>
          <button class="btn primary" type="submit" [disabled]="returnForm.invalid || busy()">Registrar devolución</button>
        </form>
      } @else {
        <div class="card muted">Un activo en estado «{{ a.status }}» no puede asignarse.</div>
      }
    }

    <div class="card">
      <h2>Historial</h2>
      <div class="table-wrap">
        <table>
          <thead>
            <tr><th>Fecha</th><th>Movimiento</th><th>Cambio</th><th>Colaborador</th><th>Usuario</th><th>Observaciones</th></tr>
          </thead>
          <tbody>
            @for (m of history()?.items ?? []; track m.id) {
              <tr>
                <td>{{ m.performedAt | date: 'dd/MM/yyyy HH:mm' }}</td>
                <td>{{ label(m) }}</td>
                <td>@if (m.previousValue || m.newValue) { {{ m.previousValue ?? '—' }} → {{ m.newValue ?? '—' }} } @else { — }</td>
                <td>{{ m.employeeName ?? '—' }}</td>
                <td>{{ m.performedBy }}</td>
                <td>{{ m.notes ?? '' }}</td>
              </tr>
            } @empty {
              <tr><td colspan="6" class="empty">Sin movimientos.</td></tr>
            }
          </tbody>
        </table>
      </div>
      @if (history(); as h) {
        <div class="pager">
          <span class="muted">{{ h.totalCount }} movimiento(s) · página {{ h.page }} de {{ h.totalPages || 1 }}</span>
          <div>
            <button class="btn small" (click)="loadHistory(h.page - 1)" [disabled]="h.page <= 1">← Anterior</button>
            <button class="btn small" (click)="loadHistory(h.page + 1)" [disabled]="h.page >= h.totalPages">Siguiente →</button>
          </div>
        </div>
      }
    </div>
  `,
})
export class AssetDetailComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly fb = inject(FormBuilder);
  private readonly route = inject(ActivatedRoute);

  private readonly id = Number(this.route.snapshot.paramMap.get('id'));
  private readonly historyPageSize = 10;

  readonly asset = signal<Asset | null>(null);
  readonly history = signal<PagedResult<AssetMovement> | null>(null);
  readonly employees = signal<Employee[]>([]);
  readonly error = signal('');
  readonly message = signal('');
  readonly busy = signal(false);

  readonly assignForm = this.fb.group({
    employeeId: [null as number | null, Validators.required],
    notes: ['', Validators.maxLength(500)],
  });

  readonly returnForm = this.fb.group({
    returnCondition: ['', [Validators.required, Validators.maxLength(200)]],
    notes: ['', Validators.maxLength(500)],
  });

  ngOnInit(): void {
    this.loadAsset();
    this.loadHistory(1);
    this.api.getActiveEmployees().subscribe({
      next: (r) => this.employees.set(r.items),
      error: (err) => this.error.set(errorMessage(err)),
    });
  }

  label(m: AssetMovement): string {
    return MOVEMENT_LABELS[m.movementType] ?? m.movementType;
  }

  assign(): void {
    const v = this.assignForm.getRawValue();
    if (this.assignForm.invalid || v.employeeId === null) return;
    this.run(this.api.assign(this.id, { employeeId: v.employeeId, notes: v.notes?.trim() || null }),
      'Activo asignado correctamente.', () => this.assignForm.reset({ employeeId: null, notes: '' }));
  }

  returnAsset(): void {
    const v = this.returnForm.getRawValue();
    if (this.returnForm.invalid) return;
    this.run(this.api.returnAsset(this.id, { returnCondition: v.returnCondition!.trim(), notes: v.notes?.trim() || null }),
      'Devolución registrada. El activo quedó disponible.', () => this.returnForm.reset({ returnCondition: '', notes: '' }));
  }

  loadHistory(page: number): void {
    this.api.getHistory(this.id, page, this.historyPageSize).subscribe({
      next: (r) => this.history.set(r),
      error: (err) => this.error.set(errorMessage(err)),
    });
  }

  private loadAsset(): void {
    this.api.getAsset(this.id).subscribe({
      next: (a) => this.asset.set(a),
      error: (err) => this.error.set(errorMessage(err)),
    });
  }

  /** Ejecuta una acción; ante éxito o error (p. ej. 409 por concurrencia) recarga el estado real del activo. */
  private run(action$: Observable<unknown>, okMessage: string, onOk: () => void): void {
    this.busy.set(true);
    this.error.set('');
    this.message.set('');
    action$.subscribe({
      next: () => {
        this.message.set(okMessage);
        onOk();
        this.finish();
      },
      error: (err) => {
        this.error.set(errorMessage(err));
        this.finish();
      },
    });
  }

  private finish(): void {
    this.busy.set(false);
    this.loadAsset();
    this.loadHistory(1);
  }
}
