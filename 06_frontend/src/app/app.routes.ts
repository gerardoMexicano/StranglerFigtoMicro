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
    path: 'system-principal',
    canActivate: [authGuard, roleGuard],
    data: { roles: ROLES_LECTURA },
    loadComponent: () =>
      import('./features/system-principal/system-principal').then((m) => m.SystemPrincipal),
  },
  {
    path: 'system-principal/reportes',
    canActivate: [authGuard, roleGuard],
    data: { roles: ROLES_LECTURA },
    loadComponent: () =>
      import('./features/system-principal/pages/reportes').then((m) => m.Reportes),
  },
  {
    path: 'system-principal/configuracion',
    canActivate: [authGuard, roleGuard],
    data: { roles: ROLES_LECTURA },
    loadComponent: () =>
      import('./features/system-principal/pages/configuracion').then((m) => m.Configuracion),
  },
  {
    path: 'system-principal/dashboard',
    canActivate: [authGuard, roleGuard],
    data: { roles: ROLES_LECTURA },
    loadComponent: () =>
      import('./features/system-principal/pages/dashboard').then((m) => m.Dashboard),
  },
  {
    path: 'second-system-broken',
    canActivate: [authGuard, roleGuard],
    data: { roles: ROLES_LECTURA },
    loadComponent: () =>
      import('./features/second-system-broken/second-system-broken').then(
        (m) => m.SecondSystemBroken,
      ),
  },
  {
    path: 'second-system-broken/pagina-antigua',
    canActivate: [authGuard, roleGuard],
    data: { roles: ROLES_LECTURA },
    loadComponent: () =>
      import('./features/second-system-broken/pages/pagina-antigua').then((m) => m.PaginaAntigua),
  },
  {
    path: 'second-system-broken/pagina-antigua-nuevo-backend',
    canActivate: [authGuard, roleGuard],
    data: { roles: ROLES_LECTURA },
    loadComponent: () =>
      import('./features/second-system-broken/pages/pagina-antigua-nuevo-backend').then(
        (m) => m.PaginaAntiguaNuevoBackend,
      ),
  },
  {
    path: 'second-system-broken/pagina-angular',
    canActivate: [authGuard, roleGuard],
    data: { roles: ROLES_LECTURA },
    loadComponent: () =>
      import('./features/second-system-broken/pages/pagina-angular').then((m) => m.PaginaAngular),
  },
  {
    path: 'legfrontnewback',
    canActivate: [authGuard, roleGuard],
    data: { roles: ROLES_LECTURA },
    loadComponent: () =>
      import('./features/legfrontnewback/legfrontnewback').then((m) => m.Legfrontnewback),
  },
  {
    // Reemplaza Usuarios (Checador de Asistencia); invitado entra a ver, no registra
    path: 'usuarios',
    canActivate: [authGuard, roleGuard],
    data: { roles: ROLES_LECTURA },
    loadComponent: () => import('./features/usuarios/usuarios').then((m) => m.Usuarios),
  },
  {
    // Reservación de salas accesible directamente en /newapp/salas desde System Principal
    path: 'salas',
    canActivate: [authGuard, roleGuard],
    data: { roles: ROLES_LECTURA },
    loadComponent: () => import('./features/salas/salas').then((m) => m.Salas),
  },
  {
    path: 'sin-acceso',
    canActivate: [authGuard],
    loadComponent: () => import('./features/sin-acceso/sin-acceso').then((m) => m.SinAcceso),
  },
  { path: '**', redirectTo: '' },
];
