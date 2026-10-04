import { Component, inject } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from './core/auth.service';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  template: `
    @if (auth.hasSession()) {
      <header class="topbar">
        <a routerLink="/assets" class="brand">IT Assets</a>
        <nav>
          <a routerLink="/assets" routerLinkActive="active" [routerLinkActiveOptions]="{ exact: true }">Activos</a>
          @if (auth.isAdmin()) {
            <a routerLink="/assets/new" routerLinkActive="active">Nuevo activo</a>
          }
        </nav>
        <div class="user">
          <span>{{ auth.username() }} <small class="muted">({{ auth.role() }})</small></span>
          <button class="btn small" (click)="auth.logout()">Salir</button>
        </div>
      </header>
    }
    <main class="container"><router-outlet /></main>
  `,
})
export class App {
  readonly auth = inject(AuthService);
}
