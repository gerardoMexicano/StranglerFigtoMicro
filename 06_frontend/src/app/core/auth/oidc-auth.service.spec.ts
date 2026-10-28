import { Location } from '@angular/common';
import { TestBed } from '@angular/core/testing';
import { LoginResponse, OidcSecurityService } from 'angular-auth-oidc-client';
import { BehaviorSubject, firstValueFrom, of, throwError } from 'rxjs';

import { OidcAuthService, RETURN_URL_KEY } from './oidc-auth.service';

function respuesta(isAuthenticated: boolean): LoginResponse {
  return {
    isAuthenticated,
    userData: null,
    accessToken: '',
    idToken: '',
    configId: '0-frontend-spa',
  };
}

describe('OidcAuthService', () => {
  let oidc: {
    isAuthenticated$: BehaviorSubject<{ isAuthenticated: boolean; allConfigsAuthenticated: [] }>;
    checkAuth: ReturnType<typeof vi.fn>;
    authorize: ReturnType<typeof vi.fn>;
    logoff: ReturnType<typeof vi.fn>;
    getAccessToken: ReturnType<typeof vi.fn>;
    forceRefreshSession: ReturnType<typeof vi.fn>;
    getPayloadFromAccessToken: ReturnType<typeof vi.fn>;
  };
  let location: { replaceState: ReturnType<typeof vi.fn> };
  let service: OidcAuthService;

  beforeEach(() => {
    sessionStorage.clear();
    oidc = {
      isAuthenticated$: new BehaviorSubject<{
        isAuthenticated: boolean;
        allConfigsAuthenticated: [];
      }>({
        isAuthenticated: false,
        allConfigsAuthenticated: [],
      }),
      checkAuth: vi.fn().mockReturnValue(of(respuesta(false))),
      authorize: vi.fn(),
      logoff: vi.fn().mockReturnValue(of(null)),
      getAccessToken: vi.fn().mockReturnValue(of('')),
      forceRefreshSession: vi.fn().mockReturnValue(of(respuesta(true))),
      getPayloadFromAccessToken: vi.fn().mockReturnValue(of(null)),
    };
    location = { replaceState: vi.fn() };

    TestBed.configureTestingModule({
      providers: [
        OidcAuthService,
        { provide: OidcSecurityService, useValue: oidc },
        { provide: Location, useValue: location },
      ],
    });
    service = TestBed.inject(OidcAuthService);
  });

  it('isAuthenticated$ refleja la sesión de la librería', async () => {
    oidc.isAuthenticated$.next({ isAuthenticated: true, allConfigsAuthenticated: [] });

    expect(await firstValueFrom(service.isAuthenticated$)).toBe(true);
  });

  describe('user$', () => {
    function conSesion(payload: unknown) {
      oidc.getPayloadFromAccessToken.mockReturnValue(of(payload));
      oidc.isAuthenticated$.next({ isAuthenticated: true, allConfigsAuthenticated: [] });
    }

    it('sin sesión es null', async () => {
      expect(await firstValueFrom(service.user$)).toBeNull();
    });

    it('con sesión sale del access token: usuario, nombre y roles de realm', async () => {
      conSesion({
        sub: 'f3a1',
        preferred_username: 'juan',
        name: 'Juan Pérez',
        realm_access: { roles: ['usuario', 'default-roles-laboratorio'] },
      });

      expect(await firstValueFrom(service.user$)).toEqual({
        userName: 'juan',
        name: 'Juan Pérez',
        roles: ['usuario', 'default-roles-laboratorio'],
      });
    });

    it('sin nombre ni roles en el token usa el usuario y una lista vacía', async () => {
      conSesion({ sub: 'f3a1', preferred_username: 'juan' });

      expect(await firstValueFrom(service.user$)).toEqual({
        userName: 'juan',
        name: 'juan',
        roles: [],
      });
    });

    it('al cerrar la sesión vuelve a null', async () => {
      conSesion({ preferred_username: 'juan' });
      oidc.isAuthenticated$.next({ isAuthenticated: false, allConfigsAuthenticated: [] });

      expect(await firstValueFrom(service.user$)).toBeNull();
    });
  });

  it('login guarda la ruta de regreso y manda a Keycloak', () => {
    service.login('/legfrontnewback?pagina=3');

    expect(sessionStorage.getItem(RETURN_URL_KEY)).toBe('/legfrontnewback?pagina=3');
    expect(oidc.authorize).toHaveBeenCalledOnce();
  });

  describe('iniciar (al arrancar la app)', () => {
    it('al volver de Keycloak con sesión, deja la URL en la ruta guardada y la olvida', async () => {
      sessionStorage.setItem(RETURN_URL_KEY, '/legfrontnewback');
      oidc.checkAuth.mockReturnValue(of(respuesta(true)));

      await service.iniciar();

      expect(location.replaceState).toHaveBeenCalledWith('/legfrontnewback');
      expect(sessionStorage.getItem(RETURN_URL_KEY)).toBeNull();
    });

    it('con sesión y sin ruta guardada, va al menú', async () => {
      oidc.checkAuth.mockReturnValue(of(respuesta(true)));

      await service.iniciar();

      expect(location.replaceState).toHaveBeenCalledWith('/menu');
    });

    it.each(['//otro.example.com/robar', 'https://otro.example.com', 'javascript:alert(1)'])(
      'ignora una ruta de regreso que no es interna (%s)',
      async (ruta) => {
        sessionStorage.setItem(RETURN_URL_KEY, ruta);
        oidc.checkAuth.mockReturnValue(of(respuesta(true)));

        await service.iniciar();

        expect(location.replaceState).toHaveBeenCalledWith('/menu');
      },
    );

    it('sin sesión no cambia la URL ni olvida la ruta guardada', async () => {
      sessionStorage.setItem(RETURN_URL_KEY, '/menu');

      await service.iniciar();

      expect(location.replaceState).not.toHaveBeenCalled();
      expect(sessionStorage.getItem(RETURN_URL_KEY)).toBe('/menu');
    });

    it('si Keycloak no responde, la app arranca igual (sin sesión)', async () => {
      oidc.checkAuth.mockReturnValue(throwError(() => new Error('sin red')));

      await expect(service.iniciar()).resolves.toBeUndefined();
      expect(location.replaceState).not.toHaveBeenCalled();
    });
  });

  it('logout cierra la sesión de Keycloak', () => {
    service.logout();

    expect(oidc.logoff).toHaveBeenCalledOnce();
  });

  it('getAccessToken devuelve null cuando la librería no tiene token', async () => {
    expect(await firstValueFrom(service.getAccessToken())).toBeNull();
  });

  it('getAccessToken devuelve el token vigente', async () => {
    oidc.getAccessToken.mockReturnValue(of('access-token'));

    expect(await firstValueFrom(service.getAccessToken())).toBe('access-token');
  });

  it('refreshSession emite true si Keycloak renueva', async () => {
    expect(await firstValueFrom(service.refreshSession())).toBe(true);
  });

  it.each([
    ['responde sin sesión', () => of(respuesta(false))],
    ['responde null', () => of(null)],
    ['falla', () => throwError(() => new Error('invalid_grant'))],
  ])('refreshSession emite false si Keycloak %s', async (_, resultado) => {
    oidc.forceRefreshSession.mockReturnValue(resultado());

    expect(await firstValueFrom(service.refreshSession())).toBe(false);
  });
});
