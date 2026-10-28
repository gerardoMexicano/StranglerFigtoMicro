import { HttpErrorResponse, HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, of, switchMap, take, throwError } from 'rxjs';

import { APP_CONFIG } from '../config/app-config';
import { AuthService } from './auth.service';

/**
 * Pone el access token solo en las peticiones a apiBaseUrl (05_backend). Ante un 401 intenta
 * renovar la sesión una vez y repite la petición; si no se puede, vuelve al login.
 */
export const apiTokenInterceptor: HttpInterceptorFn = (req, next) => {
  if (!esDeLaApi(req.url, inject(APP_CONFIG).apiBaseUrl)) {
    return next(req);
  }

  const auth = inject(AuthService);
  const router = inject(Router);

  const enviar = () =>
    auth.getAccessToken().pipe(
      take(1),
      switchMap((token) => next(conToken(req, token))),
    );

  return enviar().pipe(
    catchError((error: unknown) => {
      if (!esNoAutorizado(error)) {
        return throwError(() => error);
      }

      return auth.refreshSession().pipe(
        take(1),
        catchError(() => of(false)),
        switchMap((renovada) => (renovada ? enviar() : throwError(() => error))),
        // Sin renovación, o el token nuevo tampoco sirve: sin ciclo, de vuelta a Keycloak
        catchError((final: unknown) => {
          if (esNoAutorizado(final)) {
            auth.login(router.url);
          }
          return throwError(() => final);
        }),
      );
    }),
  );
};

function esDeLaApi(url: string, apiBaseUrl: string): boolean {
  const raiz = apiBaseUrl.replace(/\/+$/, '');
  return url === raiz || url.startsWith(`${raiz}/`);
}

function conToken(req: HttpRequest<unknown>, token: string | null): HttpRequest<unknown> {
  return token ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : req;
}

function esNoAutorizado(error: unknown): boolean {
  return error instanceof HttpErrorResponse && error.status === 401;
}
