import { Routes } from '@angular/router';

import { authGuard } from './core/auth/auth.guard';

// La app se sirve bajo /newapp/ (base href): estas rutas son /newapp/, /newapp/menu, ...
export const routes: Routes = [
  {
    path: '',
    loadComponent: () => import('./features/inicio/inicio').then((m) => m.Inicio),
  },
  {
    path: 'menu',
    canActivate: [authGuard],
    loadComponent: () => import('./features/menu/menu').then((m) => m.Menu),
  },
  {
    path: 'legfrontnewback',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/legfrontnewback/legfrontnewback').then((m) => m.Legfrontnewback),
  },
  { path: '**', redirectTo: '' },
];
