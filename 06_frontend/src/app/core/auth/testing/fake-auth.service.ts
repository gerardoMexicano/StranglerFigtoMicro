import { BehaviorSubject, Observable, of } from 'rxjs';

import { AuthService, AuthUser } from '../auth.service';

/** AuthService para pruebas: sesión controlada a mano y registro de llamadas. */
export class FakeAuthService extends AuthService {
  readonly session = new BehaviorSubject<boolean>(false);
  readonly isAuthenticated$ = this.session.asObservable();

  readonly user = new BehaviorSubject<AuthUser | null>(null);
  readonly user$ = this.user.asObservable();

  token: string | null = null;
  /** Token que deja refreshSession() cuando Keycloak sí renueva. */
  refreshedToken: string | null = null;

  readonly loginCalls: string[] = [];
  logoutCalls = 0;
  refreshCalls = 0;

  login(returnUrl: string): void {
    this.loginCalls.push(returnUrl);
  }

  logout(): void {
    this.logoutCalls++;
  }

  getAccessToken(): Observable<string | null> {
    return of(this.token);
  }

  refreshSession(): Observable<boolean> {
    this.refreshCalls++;
    if (this.refreshedToken === null) {
      return of(false);
    }
    this.token = this.refreshedToken;
    return of(true);
  }
}
