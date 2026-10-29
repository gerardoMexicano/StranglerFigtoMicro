import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { AuthService } from '../../core/auth/auth.service';
import { FakeAuthService } from '../../core/auth/testing/fake-auth.service';
import { APP_CONFIG, AppConfig } from '../../core/config/app-config';
import { Menu } from './menu';

const config: AppConfig = {
  oidc: { authority: 'http://kc/auth/realms/lab', clientId: 'frontend-spa', scope: 'openid' },
  apiBaseUrl: '/api',
  apps: {
    taxesUrl: 'http://192.168.50.10/taxes/Home/Menu',
    legacyUrl: 'http://192.168.50.10/legacy/Menu',
  },
};

describe('Menu', () => {
  let auth: FakeAuthService;

  async function pintar(): Promise<HTMLElement> {
    const fixture = TestBed.createComponent(Menu);
    await fixture.whenStable();
    return fixture.nativeElement as HTMLElement;
  }

  function enlaces(vista: HTMLElement): Record<string, string | null> {
    return Object.fromEntries(
      [...vista.querySelectorAll<HTMLAnchorElement>('.tarjeta a')].map((a) => [
        a.textContent?.trim() ?? '',
        a.getAttribute('href'),
      ]),
    );
  }

  beforeEach(() => {
    auth = new FakeAuthService();
    auth.session.next(true);
    auth.user.next({ userName: 'juan', name: 'Juan Pérez', roles: ['usuario'] });
    TestBed.configureTestingModule({
      imports: [Menu],
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: auth },
        { provide: APP_CONFIG, useValue: config },
      ],
    });
  });

  it('saluda con el nombre del usuario', async () => {
    expect((await pintar()).textContent).toContain('Juan Pérez');
  });

  it('las apps legacy abren en su URL de config.json y LEgFrontNEWBACK dentro de /newapp', async () => {
    expect(enlaces(await pintar())).toEqual({
      'Abrir BrokenTaxes': 'http://192.168.50.10/taxes/Home/Menu',
      'Abrir Legacy WebForms': 'http://192.168.50.10/legacy/Menu',
      'Abrir LEgFrontNEWBACK': '/legfrontnewback',
    });
  });

  it('un invitado solo ve LEgFrontNEWBACK', async () => {
    auth.user.next({ userName: 'ana', name: 'ana', roles: ['invitado'] });

    expect(Object.keys(enlaces(await pintar()))).toEqual(['Abrir LEgFrontNEWBACK']);
  });

  it('"Cerrar sesión" cierra la sesión de Keycloak', async () => {
    const vista = await pintar();

    [...vista.querySelectorAll('button')]
      .find((b) => b.textContent?.includes('Cerrar sesión'))
      ?.click();

    expect(auth.logoutCalls).toBe(1);
  });
});
