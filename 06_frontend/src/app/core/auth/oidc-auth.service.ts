import { Location } from '@angular/common';
import { inject, Injectable } from '@angular/core';
import { OidcSecurityService } from 'angular-auth-oidc-client';
import { catchError, firstValueFrom, map, Observable, of, switchMap } from 'rxjs';

import { AuthService, AuthUser } from './auth.service';

/** Ruta a la que vuelve el usuario después de Keycloak. No es secreta: va en sessionStorage. */
export const RETURN_URL_KEY = 'newapp.returnUrl';

const DESTINO_POR_DEFECTO = '/menu';

/** AuthService sobre angular-auth-oidc-client (Authorization Code + PKCE contra frontend-spa). */
@Injectable()
export class OidcAuthService extends AuthService {
  private readonly oidc = inject(OidcSecurityService);
  private readonly location = inject(Location);

  readonly isAuthenticated$ = this.oidc.isAuthenticated$.pipe(map((r) => r.isAuthenticated));

  readonly user$: Observable<AuthUser | null> = this.isAuthenticated$.pipe(
    switchMap((conSesion) =>
      conSesion ? this.oidc.getPayloadFromAccessToken().pipe(map(usuarioDelToken)) : of(null),
    ),
  );

  /**
   * Se corre al arrancar, antes de la primera navegación. Procesa el regreso de Keycloak
   * (?code=...&state=...) y, si hay sesión, deja la URL en la ruta que el usuario pidió:
   * el router arranca ahí y no en /newapp/, que es a donde Keycloak siempre regresa.
   */
  async iniciar(): Promise<void> {
    const { isAuthenticated } = await firstValueFrom(
      this.oidc.checkAuth().pipe(catchError(() => of({ isAuthenticated: false }))),
    );
    if (!isAuthenticated) {
      return;
    }

    const guardada = sessionStorage.getItem(RETURN_URL_KEY);
    sessionStorage.removeItem(RETURN_URL_KEY);
    this.location.replaceState(esRutaInterna(guardada) ? guardada : DESTINO_POR_DEFECTO);
  }

  login(returnUrl: string): void {
    sessionStorage.setItem(RETURN_URL_KEY, returnUrl);
    this.oidc.authorize();
  }

  logout(): void {
    this.oidc.logoff().subscribe();
  }

  getAccessToken(): Observable<string | null> {
    return this.oidc.getAccessToken().pipe(map((token) => token || null));
  }

  refreshSession(): Observable<boolean> {
    return this.oidc.forceRefreshSession().pipe(
      map((respuesta) => respuesta?.isAuthenticated === true),
      catchError(() => of(false)),
    );
  }
}

/** Claims del access token de Keycloak (scope profile + roles de realm). */
function usuarioDelToken(
  payload: {
    sub?: string;
    preferred_username?: string;
    name?: string;
    realm_access?: { roles?: string[] };
  } | null,
): AuthUser | null {
  if (!payload) {
    return null;
  }
  const userName = payload.preferred_username ?? payload.sub ?? '';
  return {
    userName,
    name: payload.name ?? userName,
    roles: payload.realm_access?.roles ?? [],
  };
}

/** Solo rutas de /newapp: nada de "//otro-host" ni esquemas como https: o javascript:. */
function esRutaInterna(ruta: string | null): ruta is string {
  return ruta !== null && ruta.startsWith('/') && !ruta.startsWith('//') && !ruta.startsWith('/\\');
}
