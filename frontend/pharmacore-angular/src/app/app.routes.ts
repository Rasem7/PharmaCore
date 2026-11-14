import { Routes } from '@angular/router';
import { authGuard } from './core/auth.guard';

export const routes: Routes = [
  { path: 'login', loadComponent: () => import('./features/login/login').then(m => m.Login) },
  {
    path: '',
    canActivate: [authGuard],
    loadComponent: () => import('./layout/shell').then(m => m.Shell),
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
      { path: 'dashboard', loadComponent: () => import('./features/dashboard/dashboard').then(m => m.Dashboard) },
      { path: 'products', loadComponent: () => import('./features/products/products').then(m => m.Products) },
      { path: 'inventory', loadComponent: () => import('./features/inventory/inventory').then(m => m.Inventory) },
      { path: 'drugs', loadComponent: () => import('./features/drugs/drugs').then(m => m.Drugs) }
    ]
  },
  { path: '**', redirectTo: '' }
];
