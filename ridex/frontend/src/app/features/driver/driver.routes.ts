import { Routes } from '@angular/router';

export const DRIVER_ROUTES: Routes = [
  {
    path: 'dashboard',
    loadComponent: () => import('./dashboard/driver-dashboard.component').then(m => m.DriverDashboardComponent)
  },
  { path: '', redirectTo: 'dashboard', pathMatch: 'full' }
];
