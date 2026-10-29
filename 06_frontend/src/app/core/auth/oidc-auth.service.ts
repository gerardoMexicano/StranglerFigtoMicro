import { Location } from '@angular/common';
import { DestroyRef, inject, Injectable } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Router } from '@angular/router';
import { OidcSecurityService } from 'angular-auth-oidc-client';
import { catchError, filter, firstValueFrom, map, Observable, of, switchMap, take } from 'rxjs';

import { AuthService, AuthUser } from './auth.service';

/** Ruta a la que vuelve el usuario después de Keycloak. No es secreta: va en sessionStorage. */
export const RETURN_URL_KEY = 'newapp.returnUrl';

const DESTINO_POR_DEFECTO = '/menu';

/** AuthService sobre angular-auth-oidc-client (Authorization Code + PKCE contra frontend-spa). */
@Injectable()
export class OidcAuthService extends AuthService {
  private readonly oidc = inject(OidcSecurityService);
  private readonly location = inject(Location);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

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
    this.cerrarAlCerrarseEnKeycloak();

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

  /**
   * Logout global: si la sesión de Keycloak se cierra desde /taxes o /legacy, el check session
   * (iframe de Keycloak, ver usarCheckSession) avisa en segundos. Se borran los tokens de memoria
   * y se vuelve a la presentación; Keycloak ya cerró su sesión, no se llama a su logout.
   * Sin check session (ng serve) la sesión acaba al fallar la siguiente renovación del token.
   */
  private cerrarAlCerrarseEnKeycloak(): void {
    this.oidc.checkSessionChanged$
      .pipe(
        filter(Boolean),
        switchMap(() => this.isAuthenticated$.pipe(take(1))),
        filter(Boolean),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe(() => {
        this.oidc.logoffLocal();
        void this.router.navigateByUrl('/');
      });
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

/**
 * El check session de OIDC carga un iframe de Keycloak que lee su cookie de sesión. Solo sirve con
 * Keycloak en el mismo origen que la app (el laboratorio: todo detrás de nginx). Desde ng serve
 * (localhost:4200) el iframe es de otro sitio, el navegador no le manda la cookie y Keycloak
 * respondería "changed" siempre: la sesión se cerraría sola.
 */
export function usarCheckSession(authority: string, origenDeLaApp: string): boolean {
  try {
    return new URL(authority).origin === origenDeLaApp;
  } catch {
    return false;
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
