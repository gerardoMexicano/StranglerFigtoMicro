import { APP_BASE_HREF } from '@angular/common';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Provider } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';

import { crearAppConfig } from '../app.config';
import { routes } from '../app.routes';
import { AuthService } from '../core/auth/auth.service';
import { FakeAuthService } from '../core/auth/testing/fake-auth.service';
import { APP_CONFIG, AppConfig } from '../core/config/app-config';
import { Salas } from '../features/salas/salas';
import { SinAcceso } from '../features/sin-acceso/sin-acceso';
import { MarcoLegacy } from './marco-legacy';
import { MONTAJE_ANGULAR_PAGE, MONTAJE_NEWAPP, montajePara } from './montaje';

const config: AppConfig = {
  oidc: { authority: 'http://kc/auth/realms/lab', clientId: 'frontend-spa', scope: 'openid' },
  apiBaseUrl: '/api',
  apps: { taxesUrl: '/taxes/Home/Menu', legacyUrl: '/legacy/Menu' },
};

describe('montajePara', () => {
  it.each([
    '/legacy/AngularPage',
    '/legacy/AngularPage/',
    '/legacy/AngularPage/sin-acceso',
    '/legacy/angularpage',
    '/LEGACY/ANGULARPAGE/',
  ])('%s es /legacy/AngularPage', (pathname) => {
    expect(montajePara(pathname)).toBe(MONTAJE_ANGULAR_PAGE);
  });

  it.each(['/newapp/', '/newapp/menu', '/newapp/legacy/AngularPage', '/legacy/AngularPages', '/'])(
    '%s es /newapp',
    (pathname) => {
      expect(montajePara(pathname)).toBe(MONTAJE_NEWAPP);
    },
  );

  it('/newapp no cambia: sus rutas y el <base href> de siempre', () => {
    expect(MONTAJE_NEWAPP).toEqual({ baseHref: null, routes });
  });
});

describe('crearAppConfig', () => {
  /** Los providers de primer nivel que proveen un token, sin correr la app. */
  function proveedoresDe(token: unknown, providers: readonly unknown[]): Provider[] {
    return providers.filter(
      (p): p is Provider =>
        typeof p === 'object' && p !== null && 'provide' in p && p.provide === token,
    ) as Provider[];
  }

  it('en /legacy/AngularPage el router usa esa ruta como base', () => {
    const { providers } = crearAppConfig(config, MONTAJE_ANGULAR_PAGE);

    expect(proveedoresDe(APP_BASE_HREF, providers)).toEqual([
      { provide: APP_BASE_HREF, useValue: '/legacy/AngularPage/' },
    ]);
  });

  it('en /newapp no toca APP_BASE_HREF: sale del <base href>', () => {
    expect(proveedoresDe(APP_BASE_HREF, crearAppConfig(config).providers)).toEqual([]);
  });
});

describe('rutas de /legacy/AngularPage', () => {
  let auth: FakeAuthService;

  beforeEach(() => {
    auth = new FakeAuthService();
    TestBed.configureTestingModule({
      providers: [
        provideRouter(MONTAJE_ANGULAR_PAGE.routes),
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: APP_CONFIG, useValue: config },
        { provide: AuthService, useValue: auth },
      ],
    });
  });

  /** El componente de la ruta hija, dentro del marco (routeDebugElement es el marco). */
  function pintado(
    harness: RouterTestingHarness,
    tipo: abstract new (...args: never[]) => unknown,
  ) {
    harness.detectChanges();
    return harness.fixture.debugElement.query((d) => d.componentInstance instanceof tipo);
  }

  function conSesion(roles: string[]) {
    auth.session.next(true);
    auth.user.next({ userName: 'juan', name: 'Juan Pérez', roles });
  }

  it('la raíz es la Reservación de salas dentro del marco de WebForms', async () => {
    conSesion(['usuario']);
    const harness = await RouterTestingHarness.create();

    await harness.navigateByUrl('/');

    expect(harness.routeDebugElement?.componentInstance).toBeInstanceOf(MarcoLegacy);
    expect(pintado(harness, Salas)).not.toBeNull();
    expect(document.title).toBe('Reservación de salas - Sistema Laboratorio');
    TestBed.inject(HttpTestingController).match(() => true);
  });

  it('invitado también entra: solo consulta', async () => {
    conSesion(['invitado']);
    const harness = await RouterTestingHarness.create();

    await harness.navigateByUrl('/');

    expect(pintado(harness, Salas)).not.toBeNull();
    TestBed.inject(HttpTestingController).match(() => true);
  });

  it('sin sesión manda a Keycloak para volver a la raíz del montaje', async () => {
    const harness = await RouterTestingHarness.create();

    await harness.navigateByUrl('/');

    expect(auth.loginCalls).toEqual(['/']);
  });

  it('sin ningún rol del realm va a sin-acceso, también dentro del marco', async () => {
    conSesion([]);
    const harness = await RouterTestingHarness.create();

    await harness.navigateByUrl('/');

    expect(TestBed.inject(Router).url).toBe('/sin-acceso');
    expect(harness.routeDebugElement?.componentInstance).toBeInstanceOf(MarcoLegacy);
    expect(pintado(harness, SinAcceso)).not.toBeNull();
  });

  it('cualquier otra ruta (p. ej. /menu, que es de /newapp) vuelve a la raíz', async () => {
    conSesion(['usuario']);
    const harness = await RouterTestingHarness.create();

    await harness.navigateByUrl('/menu');

    expect(TestBed.inject(Router).url).toBe('/');
    TestBed.inject(HttpTestingController).match(() => true);
  });
});
