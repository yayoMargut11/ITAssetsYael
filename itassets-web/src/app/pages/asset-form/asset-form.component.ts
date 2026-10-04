import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { ApiService } from '../../core/api.service';
import { errorMessage } from '../../core/error.util';
import { CATEGORIES, CreateAssetRequest, OWNERSHIP_TYPES, Supplier } from '../../core/models';

@Component({
  selector: 'app-asset-form',
  imports: [ReactiveFormsModule, RouterLink],
  template: `
    <div class="page-head">
      <h1>Nuevo activo</h1>
      <a routerLink="/assets" class="btn">← Volver</a>
    </div>

    @if (error()) { <div class="alert error">{{ error() }}</div> }

    <form class="card form-grid" [formGroup]="form" (ngSubmit)="submit()">
      <label>Código de activo *
        <input formControlName="assetCode" maxlength="30" placeholder="TI-0001010" />
        @if (invalid('assetCode')) { <small class="field-error">Obligatorio. Solo letras, números, '-' y '_' (máx. 30).</small> }
      </label>

      <label>Número de serie
        <input formControlName="serialNumber" maxlength="100" />
      </label>

      <label>Categoría *
        <input formControlName="category" maxlength="50" list="categories" />
        <datalist id="categories">@for (c of categories; track c) { <option [value]="c"></option> }</datalist>
        @if (invalid('category')) { <small class="field-error">Obligatorio (máx. 50).</small> }
      </label>

      <label>Marca *
        <input formControlName="brand" maxlength="50" />
        @if (invalid('brand')) { <small class="field-error">Obligatorio (máx. 50).</small> }
      </label>

      <label>Modelo *
        <input formControlName="model" maxlength="80" />
        @if (invalid('model')) { <small class="field-error">Obligatorio (máx. 80).</small> }
      </label>

      <label>Tipo de propiedad *
        <select formControlName="ownershipType">
          @for (o of ownershipTypes; track o) { <option [value]="o">{{ o }}</option> }
        </select>
      </label>

      <label>Proveedor @if (isRented()) { * }
        <select formControlName="supplierId">
          <option [ngValue]="null">— Sin proveedor —</option>
          @for (s of suppliers(); track s.id) { <option [ngValue]="s.id">{{ s.name }}</option> }
        </select>
        @if (invalid('supplierId')) { <small class="field-error">Un activo arrendado debe tener proveedor.</small> }
      </label>

      <label>Ubicación actual
        <input formControlName="currentLocation" maxlength="100" />
      </label>

      <label>Fecha de compra
        <input type="date" formControlName="purchaseDate" />
      </label>

      @if (isRented()) {
        <label>Fin de renta
          <input type="date" formControlName="rentalEndDate" />
        </label>
      }

      <div class="form-actions">
        <button class="btn primary" type="submit" [disabled]="saving()">{{ saving() ? 'Guardando…' : 'Guardar activo' }}</button>
      </div>
    </form>
  `,
})
export class AssetFormComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly api = inject(ApiService);
  private readonly router = inject(Router);

  readonly categories = CATEGORIES;
  readonly ownershipTypes = OWNERSHIP_TYPES;
  readonly suppliers = signal<Supplier[]>([]);
  readonly saving = signal(false);
  readonly error = signal('');
  readonly isRented = signal(false);

  readonly form = this.fb.group({
    assetCode: ['', [Validators.required, Validators.maxLength(30), Validators.pattern(/^[A-Za-z0-9_-]+$/)]],
    serialNumber: ['', Validators.maxLength(100)],
    category: ['', [Validators.required, Validators.maxLength(50)]],
    brand: ['', [Validators.required, Validators.maxLength(50)]],
    model: ['', [Validators.required, Validators.maxLength(80)]],
    ownershipType: ['Propio', Validators.required],
    supplierId: [null as number | null],
    currentLocation: ['', Validators.maxLength(100)],
    purchaseDate: [''],
    rentalEndDate: [''],
  });

  ngOnInit(): void {
    this.api.getSuppliers().subscribe({
      next: (r) => this.suppliers.set(r.items.filter((s) => s.isActive)),
      error: (err) => this.error.set(errorMessage(err)),
    });

    this.form.controls.ownershipType.valueChanges.subscribe((v) => this.applyOwnership(v));
    this.applyOwnership(this.form.controls.ownershipType.value);
  }

  invalid(name: string): boolean {
    const c = this.form.get(name);
    return !!c && c.invalid && (c.touched || c.dirty);
  }

  submit(): void {
    this.form.markAllAsTouched();
    if (this.form.invalid) return;

    const v = this.form.getRawValue();
    const body: CreateAssetRequest = {
      assetCode: v.assetCode!.trim(),
      serialNumber: v.serialNumber?.trim() || null,
      category: v.category!.trim(),
      brand: v.brand!.trim(),
      model: v.model!.trim(),
      ownershipType: v.ownershipType!,
      supplierId: v.supplierId,
      currentLocation: v.currentLocation?.trim() || null,
      purchaseDate: v.purchaseDate || null,
      rentalEndDate: this.isRented() ? v.rentalEndDate || null : null,
    };

    this.saving.set(true);
    this.error.set('');
    this.api.createAsset(body).subscribe({
      next: (asset) => this.router.navigate(['/assets', asset.id]),
      error: (err) => {
        this.error.set(errorMessage(err));
        this.saving.set(false);
      },
    });
  }

  private applyOwnership(type: string | null): void {
    const rented = type === 'Arrendado';
    this.isRented.set(rented);
    const supplier = this.form.controls.supplierId;
    supplier.setValidators(rented ? [Validators.required] : []);
    supplier.updateValueAndValidity();
  }
}
