import { Routes } from '@angular/router';

import { authGuard } from '../core/auth/auth.guard';
import { roleGuard } from '../core/auth/role.guard';
import { ROLES_LECTURA } from '../core/auth/roles';

// Rutas de /legacy/AngularPage/ (APP_BASE_HREF del montaje): todo va dentro del marco que imita
// el Site.Master de WebForms. La raíz es la Reservación de salas; invitado entra a ver, no reserva.
export const angularPageRoutes: Routes = [
  {
    path: '',
    loadComponent: () => import('./marco-legacy').then((m) => m.MarcoLegacy),
    children: [
      {
        path: '',
        title: 'Reservación de salas - Sistema Laboratorio',
        canActivate: [authGuard, roleGuard],
        data: { roles: ROLES_LECTURA },
        loadComponent: () => import('../features/salas/salas').then((m) => m.Salas),
      },
      {
        path: 'sin-acceso',
        title: 'Sin acceso - Sistema Laboratorio',
        canActivate: [authGuard],
        loadComponent: () => import('../features/sin-acceso/sin-acceso').then((m) => m.SinAcceso),
      },
    ],
  },
  { path: '**', redirectTo: '' },
];
