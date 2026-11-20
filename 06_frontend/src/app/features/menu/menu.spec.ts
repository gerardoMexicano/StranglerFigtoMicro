import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { AuthService } from '../../core/auth/auth.service';
import { FakeAuthService } from '../../core/auth/testing/fake-auth.service';
import { Menu } from './menu';

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
      providers: [provideRouter([]), { provide: AuthService, useValue: auth }],
    });
  });

  it('saluda con el nombre del usuario', async () => {
    expect((await pintar()).textContent).toContain('Juan Pérez');
  });

  it('muestra únicamente los dos accesos a los menús Angular: System Principal y Second System Broken', async () => {
    expect(enlaces(await pintar())).toEqual({
      'Abrir System Principal': '/system-principal',
      'Abrir Second System Broken': '/second-system-broken',
    });
  });

  it('no muestra ninguna referencia a Legacy WebForms ni a BrokenTaxes', async () => {
    const texto = (await pintar()).textContent ?? '';

    expect(texto).not.toContain('Legacy WebForms');
    expect(texto).not.toContain('BrokenTaxes');
    expect(texto).not.toContain('IIS');
  });

  it('"Cerrar sesión" cierra la sesión de Keycloak', async () => {
    const vista = await pintar();

    [...vista.querySelectorAll('button')]
      .find((b) => b.textContent?.includes('Cerrar sesión'))
      ?.click();

    expect(auth.logoutCalls).toBe(1);
  });
});
