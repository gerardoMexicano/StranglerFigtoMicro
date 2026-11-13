import { Routes } from '@angular/router';

import { authGuard } from '../core/auth/auth.guard';
import { roleGuard } from '../core/auth/role.guard';
import { ROLES_LECTURA } from '../core/auth/roles';

// Rutas de /legacy/Usuarios/ (APP_BASE_HREF del montaje): la URL de Usuarios.aspx sigue existiendo,
// ahora con el Checador de Asistencia de Angular dentro del marco de WebForms. Es la misma vista
// que /newapp/usuarios; aquí el regreso al menú lo da el marco (enMarcoLegacy).
export const usuariosLegacyRoutes: Routes = [
  {
    path: '',
    loadComponent: () => import('./marco-legacy').then((m) => m.MarcoLegacy),
    children: [
      {
        path: '',
        title: 'Usuarios - Checador - Sistema Laboratorio',
        canActivate: [authGuard, roleGuard],
        data: { roles: ROLES_LECTURA, enMarcoLegacy: true },
        loadComponent: () => import('../features/usuarios/usuarios').then((m) => m.Usuarios),
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
