import { Routes } from '@angular/router';

export const USER_ROUTES: Routes = [
  {
    path: 'dashboard',
    loadComponent: () => import('./dashboard/dashboard.component').then(m => m.UserDashboardComponent)
  },
  { path: '', redirectTo: 'dashboard', pathMatch: 'full' }
];
