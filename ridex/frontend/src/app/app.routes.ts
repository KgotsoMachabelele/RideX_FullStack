import { Routes } from '@angular/router';
import { authGuard, userGuard, driverGuard } from './core/guards/auth.guard';

export const routes: Routes = [
  { path: '', redirectTo: 'auth/login', pathMatch: 'full' },

  // Auth (lazy-loaded)
  {
    path: 'auth',
    loadChildren: () => import('./features/auth/auth.routes').then(m => m.AUTH_ROUTES)
  },

  // User portal
  {
    path: 'user',
    canActivate: [authGuard, userGuard],
    loadChildren: () => import('./features/user/user.routes').then(m => m.USER_ROUTES)
  },

  // Driver portal
  {
    path: 'driver',
    canActivate: [authGuard, driverGuard],
    loadChildren: () => import('./features/driver/driver.routes').then(m => m.DRIVER_ROUTES)
  },

  { path: '**', redirectTo: 'auth/login' }
];
