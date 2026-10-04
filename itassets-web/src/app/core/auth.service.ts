import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { tap } from 'rxjs';
import { LoginResponse } from './models';

const STORAGE_KEY = 'itassets.session';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);

  private readonly _session = signal<LoginResponse | null>(this.load());

  readonly session = this._session.asReadonly();
  readonly hasSession = computed(() => this._session() !== null);
  readonly username = computed(() => this._session()?.username ?? '');
  readonly role = computed(() => this._session()?.role ?? '');
  readonly isAdmin = computed(() => this._session()?.role === 'Administrador');

  login(username: string, password: string) {
    return this.http
      .post<LoginResponse>('/api/auth/login', { username, password })
      .pipe(tap((res) => this.save(res)));
  }

  /** Válido solo si hay sesión y el token no ha expirado (la API también lo valida en cada petición). */
  isAuthenticated(): boolean {
    const s = this._session();
    if (!s) return false;
    if (new Date(s.expiresAtUtc).getTime() <= Date.now()) {
      this.clear();
      return false;
    }
    return true;
  }

  token(): string | null {
    return this._session()?.token ?? null;
  }

  logout(sessionExpired = false): void {
    this.clear();
    this.router.navigate(['/login'], sessionExpired ? { queryParams: { expired: 1 } } : {});
  }

  // sessionStorage: la sesión se pierde al cerrar la pestaña (más seguro que localStorage).
  private save(res: LoginResponse): void {
    sessionStorage.setItem(STORAGE_KEY, JSON.stringify(res));
    this._session.set(res);
  }

  private clear(): void {
    sessionStorage.removeItem(STORAGE_KEY);
    this._session.set(null);
  }

  private load(): LoginResponse | null {
    try {
      const raw = sessionStorage.getItem(STORAGE_KEY);
      return raw ? (JSON.parse(raw) as LoginResponse) : null;
    } catch {
      return null;
    }
  }
}
