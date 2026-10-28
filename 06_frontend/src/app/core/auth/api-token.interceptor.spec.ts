import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';

import { APP_CONFIG, AppConfig } from '../config/app-config';
import { apiTokenInterceptor } from './api-token.interceptor';
import { AuthService } from './auth.service';
import { FakeAuthService } from './testing/fake-auth.service';

const config: AppConfig = {
  oidc: {
    authority: 'http://192.168.50.10/auth/realms/laboratorio',
    clientId: 'frontend-spa',
    scope: 'openid profile',
  },
  apiBaseUrl: '/api',
  apps: { taxesUrl: '/taxes/Home/Menu', legacyUrl: '/legacy/Menu' },
};

describe('apiTokenInterceptor', () => {
  let auth: FakeAuthService;
  let http: HttpClient;
  let backend: HttpTestingController;

  beforeEach(() => {
    auth = new FakeAuthService();
    auth.token = 'token-1';

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([apiTokenInterceptor])),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: APP_CONFIG, useValue: config },
        { provide: AuthService, useValue: auth },
      ],
    });

    http = TestBed.inject(HttpClient);
    backend = TestBed.inject(HttpTestingController);
  });

  afterEach(() => backend.verify());

  describe('a qué peticiones les pone el token', () => {
    it('a las de /api les agrega Authorization: Bearer', () => {
      http.get('/api/sales/orders').subscribe();

      const req = backend.expectOne('/api/sales/orders');
      expect(req.request.headers.get('Authorization')).toBe('Bearer token-1');
      req.flush({});
    });

    it.each([
      ['Keycloak', '/auth/realms/laboratorio/protocol/openid-connect/token'],
      ['otro dominio', 'https://otro.example.com/api/sales/orders'],
      ['un prefijo parecido', '/api-docs/v1.json'],
      ['config.json', '/newapp/config.json'],
    ])('a %s no le pone el token', (_, url) => {
      http.get(url).subscribe();

      const req = backend.expectOne(url);
      expect(req.request.headers.has('Authorization')).toBe(false);
      req.flush({});
    });

    it('sin sesión manda la petición a /api sin encabezado (el backend responde 401)', () => {
      auth.token = null;
      http.get('/api/sales/orders').subscribe({ error: () => {} });

      const req = backend.expectOne('/api/sales/orders');
      expect(req.request.headers.has('Authorization')).toBe(false);
      req.flush({});
    });
  });

  describe('cuando /api responde 401', () => {
    it('renueva la sesión una vez y repite la petición con el token nuevo', async () => {
      auth.refreshedToken = 'token-2';
      const respuesta = firstValueFrom(http.get<{ ok: boolean }>('/api/sales/orders'));

      backend
        .expectOne('/api/sales/orders')
        .flush(null, { status: 401, statusText: 'Unauthorized' });
      const repetida = backend.expectOne('/api/sales/orders');
      expect(repetida.request.headers.get('Authorization')).toBe('Bearer token-2');
      repetida.flush({ ok: true });

      expect(await respuesta).toEqual({ ok: true });
      expect(auth.refreshCalls).toBe(1);
      expect(auth.loginCalls).toEqual([]);
    });

    it('si Keycloak ya no renueva, vuelve al login desde la ruta actual y propaga el error', async () => {
      await TestBed.inject(Router).navigateByUrl('/');
      auth.refreshedToken = null;
      const respuesta = firstValueFrom(http.get('/api/sales/orders'));

      backend
        .expectOne('/api/sales/orders')
        .flush(null, { status: 401, statusText: 'Unauthorized' });

      await expect(respuesta).rejects.toMatchObject({ status: 401 });
      expect(auth.refreshCalls).toBe(1);
      expect(auth.loginCalls).toEqual(['/']);
    });

    it('si la petición repetida también da 401, no entra en ciclo: vuelve al login', async () => {
      auth.refreshedToken = 'token-2';
      const respuesta = firstValueFrom(http.get('/api/sales/orders'));

      backend
        .expectOne('/api/sales/orders')
        .flush(null, { status: 401, statusText: 'Unauthorized' });
      backend
        .expectOne('/api/sales/orders')
        .flush(null, { status: 401, statusText: 'Unauthorized' });

      await expect(respuesta).rejects.toMatchObject({ status: 401 });
      expect(auth.refreshCalls).toBe(1);
      expect(auth.loginCalls).toHaveLength(1);
    });
  });

  it('otros errores de /api (500) se propagan sin renovar ni volver al login', async () => {
    const respuesta = firstValueFrom(http.get('/api/sales/orders'));

    backend.expectOne('/api/sales/orders').flush(null, { status: 500, statusText: 'Server Error' });

    await expect(respuesta).rejects.toMatchObject({ status: 500 });
    expect(auth.refreshCalls).toBe(0);
    expect(auth.loginCalls).toEqual([]);
  });

  it('un 401 fuera de /api no dispara la renovación', async () => {
    const respuesta = firstValueFrom(http.get('/newapp/config.json'));

    backend
      .expectOne('/newapp/config.json')
      .flush(null, { status: 401, statusText: 'Unauthorized' });

    await expect(respuesta).rejects.toMatchObject({ status: 401 });
    expect(auth.refreshCalls).toBe(0);
  });
});
