import { Observable } from 'rxjs';

/** Quién inició sesión, tomado del access token de Keycloak. */
export interface AuthUser {
  /** preferred_username: el usuario del legacy */
  userName: string;
  /** Nombre para mostrar; si Keycloak no lo trae, el userName */
  name: string;
  /** Roles de realm (realm_access.roles) */
  roles: string[];
}

/**
 * Lo que la app necesita de la sesión. Guard, interceptor y vistas dependen de esta clase,
 * no de angular-auth-oidc-client: la implementación que la envuelve se registra en app.config.
 */
export abstract class AuthService {
  abstract readonly isAuthenticated$: Observable<boolean>;

  /** Usuario de la sesión o null sin sesión. */
  abstract readonly user$: Observable<AuthUser | null>;

  /** Manda a Keycloak; al volver, la app regresa a returnUrl. */
  abstract login(returnUrl: string): void;

  abstract logout(): void;

  /** Access token vigente (solo en memoria) o null si no hay sesión. */
  abstract getAccessToken(): Observable<string | null>;

  /** Pide un access token nuevo con el refresh token. Emite false si Keycloak ya no lo renueva. */
  abstract refreshSession(): Observable<boolean>;
}
