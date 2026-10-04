import { Routes } from '@angular/router';
import { adminGuard, authGuard } from './core/guards';
import { AssetDetailComponent } from './pages/asset-detail/asset-detail.component';
import { AssetFormComponent } from './pages/asset-form/asset-form.component';
import { AssetsListComponent } from './pages/assets-list/assets-list.component';
import { LoginComponent } from './pages/login/login.component';

export const routes: Routes = [
  { path: 'login', component: LoginComponent },
  { path: 'assets', component: AssetsListComponent, canActivate: [authGuard] },
  // 'new' debe ir antes que ':id'
  { path: 'assets/new', component: AssetFormComponent, canActivate: [authGuard, adminGuard] },
  { path: 'assets/:id', component: AssetDetailComponent, canActivate: [authGuard] },
  { path: '', pathMatch: 'full', redirectTo: 'assets' },
  { path: '**', redirectTo: 'assets' },
];
