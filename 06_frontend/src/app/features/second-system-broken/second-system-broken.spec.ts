import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { AuthService } from '../../core/auth/auth.service';
import { FakeAuthService } from '../../core/auth/testing/fake-auth.service';
import { SecondSystemBroken } from './second-system-broken';

describe('SecondSystemBroken', () => {
  let auth: FakeAuthService;

  async function pintar(): Promise<HTMLElement> {
    const fixture = TestBed.createComponent(SecondSystemBroken);
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
      imports: [SecondSystemBroken],
      providers: [provideRouter([]), { provide: AuthService, useValue: auth }],
    });
  });

  it('saluda al usuario y ofrece enlace para volver al menú principal', async () => {
    const vista = await pintar();

    expect(vista.textContent).toContain('Second System Broken');
    expect(vista.textContent).toContain('Juan Pérez');
    const volver = [...vista.querySelectorAll('a')].find((a) => a.textContent?.includes('Volver'));
    expect(volver?.getAttribute('href')).toBe('/menu');
  });

  it('muestra los 4 botones del segundo sistema redirigiendo a hojas Angular', async () => {
    expect(enlacesTarjetas(await pintar())).toEqual({
      'Ir a Página Antigua': '/second-system-broken/pagina-antigua',
      'Ir a Backend Nuevo': '/second-system-broken/pagina-antigua-nuevo-backend',
      'Ir a Angular': '/second-system-broken/pagina-angular',
      'Ir a System Principal': '/system-principal',
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
