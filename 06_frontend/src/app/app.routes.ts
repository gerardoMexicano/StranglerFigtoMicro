import { Routes } from '@angular/router';

import { authGuard } from './core/auth/auth.guard';
import { roleGuard } from './core/auth/role.guard';
import { ROLES_LECTURA } from './core/auth/roles';

// La app se sirve bajo /newapp/ (base href): estas rutas son /newapp/, /newapp/menu, ...
// authGuard va primero: sin sesión gana el login y no /sin-acceso.
export const routes: Routes = [
  {
    path: '',
    loadComponent: () => import('./features/inicio/inicio').then((m) => m.Inicio),
  },
  {
    path: 'menu',
    canActivate: [authGuard, roleGuard],
    data: { roles: ROLES_LECTURA },
    loadComponent: () => import('./features/menu/menu').then((m) => m.Menu),
  },
  {
    path: 'legfrontnewback',
    canActivate: [authGuard, roleGuard],
    data: { roles: ROLES_LECTURA },
    loadComponent: () =>
      import('./features/legfrontnewback/legfrontnewback').then((m) => m.Legfrontnewback),
  },
  {
    // Reemplaza /legacy/Usuarios.aspx (Checador de Asistencia); invitado entra a ver, no registra
    path: 'usuarios',
    canActivate: [authGuard, roleGuard],
    data: { roles: ROLES_LECTURA },
    loadComponent: () => import('./features/usuarios/usuarios').then((m) => m.Usuarios),
  },
  {
    path: 'sin-acceso',
    canActivate: [authGuard],
    loadComponent: () => import('./features/sin-acceso/sin-acceso').then((m) => m.SinAcceso),
  },
  { path: '**', redirectTo: '' },
];
