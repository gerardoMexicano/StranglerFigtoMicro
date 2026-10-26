import { Observable } from 'rxjs';

/**
 * Lo que la app necesita de la sesión. Guard, interceptor y vistas dependen de esta clase,
 * no de angular-auth-oidc-client: la implementación que la envuelve se registra en app.config.
 */
export abstract class AuthService {
  abstract readonly isAuthenticated$: Observable<boolean>;

  /** Manda a Keycloak; al volver, la app regresa a returnUrl. */
  abstract login(returnUrl: string): void;

  abstract logout(): void;

  /** Access token vigente (solo en memoria) o null si no hay sesión. */
  abstract getAccessToken(): Observable<string | null>;

  /** Pide un access token nuevo con el refresh token. Emite false si Keycloak ya no lo renueva. */
  abstract refreshSession(): Observable<boolean>;
}
