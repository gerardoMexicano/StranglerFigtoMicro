import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { AuthService } from '../../core/auth/auth.service';
import { FakeAuthService } from '../../core/auth/testing/fake-auth.service';
import { Inicio } from './inicio';

describe('Inicio (presentación)', () => {
  let auth: FakeAuthService;

  async function pintar(): Promise<HTMLElement> {
    const fixture = TestBed.createComponent(Inicio);
    await fixture.whenStable();
    return fixture.nativeElement as HTMLElement;
  }

  beforeEach(() => {
    auth = new FakeAuthService();
    TestBed.configureTestingModule({
      imports: [Inicio],
      providers: [provideRouter([]), { provide: AuthService, useValue: auth }],
    });
  });

  it('sin sesión, "Entrar" manda a Keycloak para volver al menú', async () => {
    const vista = await pintar();

    const entrar = [...vista.querySelectorAll('button')].find(
      (b) => b.textContent?.trim() === 'Entrar',
    );
    entrar?.click();

    expect(entrar).toBeDefined();
    expect(auth.loginCalls).toEqual(['/menu']);
  });

  it('con sesión ofrece ir directo al menú, sin volver a Keycloak', async () => {
    auth.session.next(true);
    const vista = await pintar();

    const irAlMenu = vista.querySelector<HTMLAnchorElement>('a.boton');
    expect(irAlMenu?.getAttribute('href')).toBe('/menu');
    expect(vista.querySelector('button')).toBeNull();
  });
});
