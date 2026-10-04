import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { AuthService } from '../../core/auth.service';
import { errorMessage } from '../../core/error.util';

@Component({
  selector: 'app-login',
  imports: [ReactiveFormsModule],
  template: `
    <div class="login-wrap">
      <form class="card login-card" [formGroup]="form" (ngSubmit)="submit()">
        <h1>IT Assets</h1>
        <p class="muted">Inicia sesión para continuar</p>

        @if (expired) {
          <div class="alert warn">Tu sesión expiró. Inicia sesión de nuevo.</div>
        }
        @if (error()) {
          <div class="alert error">{{ error() }}</div>
        }

        <label>Usuario
          <input formControlName="username" autocomplete="username" maxlength="100" />
        </label>
        <label>Contraseña
          <input type="password" formControlName="password" autocomplete="current-password" maxlength="200" />
        </label>

        <button class="btn primary" type="submit" [disabled]="form.invalid || loading()">
          {{ loading() ? 'Ingresando…' : 'Ingresar' }}
        </button>
      </form>
    </div>
  `,
})
export class LoginComponent {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  readonly form = this.fb.nonNullable.group({
    username: ['', [Validators.required, Validators.maxLength(100)]],
    password: ['', [Validators.required, Validators.maxLength(200)]],
  });
  readonly loading = signal(false);
  readonly error = signal('');
  readonly expired = this.route.snapshot.queryParamMap.has('expired');

  submit(): void {
    if (this.form.invalid) return;
    this.loading.set(true);
    this.error.set('');
    const { username, password } = this.form.getRawValue();
    this.auth.login(username.trim(), password).subscribe({
      next: () => {
        const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl');
        // Solo rutas internas (evita open-redirect)
        this.router.navigateByUrl(returnUrl && returnUrl.startsWith('/') && !returnUrl.startsWith('//') ? returnUrl : '/assets');
      },
      error: (err) => {
        this.error.set(errorMessage(err));
        this.loading.set(false);
      },
    });
  }
}
