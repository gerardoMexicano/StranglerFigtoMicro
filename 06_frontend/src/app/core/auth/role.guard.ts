import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { map, take } from 'rxjs';

import { AuthService } from './auth.service';

/**
 * Pide uno de los roles de data.roles (vacío = cualquiera con sesión); sin ninguno, a /sin-acceso.
 * Va después de authGuard: sin sesión no hace nada y deja que authGuard mande al login.
 * Solo oculta pantallas: quien protege los datos es el backend (política Lectura en /api).
 */
export const roleGuard: CanActivateFn = (route) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  const permitidos: readonly string[] = route.data?.['roles'] ?? [];

  return auth.user$.pipe(
    take(1),
    map((usuario) => {
      if (!usuario) {
        return false;
      }
      if (permitidos.length === 0 || permitidos.some((rol) => usuario.roles.includes(rol))) {
        return true;
      }
      return router.createUrlTree(['/sin-acceso']);
    }),
  );
};
