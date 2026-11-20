import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { AuthService } from '../../core/auth/auth.service';
import { FakeAuthService } from '../../core/auth/testing/fake-auth.service';
import { SystemPrincipal } from './system-principal';

describe('SystemPrincipal', () => {
  let auth: FakeAuthService;

  async function pintar(): Promise<HTMLElement> {
    const fixture = TestBed.createComponent(SystemPrincipal);
    await fixture.whenStable();
    return fixture.nativeElement as HTMLElement;
  }

  function enlacesTarjetas(vista: HTMLElement): Record<string, string | null> {
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
      imports: [SystemPrincipal],
      providers: [provideRouter([]), { provide: AuthService, useValue: auth }],
    });
  });

  it('saluda al usuario y ofrece enlace para volver al menú principal', async () => {
    const vista = await pintar();

    expect(vista.textContent).toContain('System Principal');
    expect(vista.textContent).toContain('Juan Pérez');
    const volver = [...vista.querySelectorAll('a')].find((a) => a.textContent?.includes('Volver'));
    expect(volver?.getAttribute('href')).toBe('/menu');
  });

  it('muestra los 7 botones del sistema principal redirigiendo a hojas Angular o módulos ya migrados', async () => {
    expect(enlacesTarjetas(await pintar())).toEqual({
      'Ir a Reportes': '/system-principal/reportes',
      'Ir a Usuarios': '/usuarios',
      'Ir a Configuración': '/system-principal/configuracion',
      'Ir a Dashboard': '/system-principal/dashboard',
      'Ir a Second System Broken': '/second-system-broken',
      'Ir a Reservación de salas': '/salas',
      'Ir a LEgFrontNEWBACK': '/legfrontnewback',
    });
  });

  it('"Cerrar sesión" cierra la sesión de Keycloak', async () => {
    const vista = await pintar();

    [...vista.querySelectorAll('button')]
      .find((b) => b.textContent?.includes('Cerrar sesión'))
      ?.click();

    expect(auth.logoutCalls).toBe(1);
  });
});
