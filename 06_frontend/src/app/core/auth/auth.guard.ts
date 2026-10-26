import { inject } from '@angular/core';
import { CanActivateFn } from '@angular/router';
import { map, take } from 'rxjs';

import { AuthService } from './auth.service';

/** Sin sesión manda a Keycloak recordando a dónde iba el usuario. */
export const authGuard: CanActivateFn = (_route, state) => {
  const auth = inject(AuthService);

  return auth.isAuthenticated$.pipe(
    take(1),
    map((conSesion) => {
      if (!conSesion) {
        auth.login(state.url);
      }
      return conSesion;
    }),
  );
};
