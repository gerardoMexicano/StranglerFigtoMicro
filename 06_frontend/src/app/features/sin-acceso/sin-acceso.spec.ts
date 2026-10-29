import { TestBed } from '@angular/core/testing';

import { AuthService } from '../../core/auth/auth.service';
import { FakeAuthService } from '../../core/auth/testing/fake-auth.service';
import { SinAcceso } from './sin-acceso';

describe('SinAcceso', () => {
  let auth: FakeAuthService;

  async function pintar(): Promise<HTMLElement> {
    const fixture = TestBed.createComponent(SinAcceso);
    await fixture.whenStable();
    return fixture.nativeElement as HTMLElement;
  }

  beforeEach(() => {
    auth = new FakeAuthService();
    auth.session.next(true);
    auth.user.next({ userName: 'ana', name: 'Ana López', roles: [] });
    TestBed.configureTestingModule({
      imports: [SinAcceso],
      providers: [{ provide: AuthService, useValue: auth }],
    });
  });

  it('explica que el usuario no tiene permisos y quién es', async () => {
    const texto = (await pintar()).textContent ?? '';

    expect(texto).toContain('No tienes acceso');
    expect(texto).toContain('Ana López');
  });

  it('"Cerrar sesión" cierra la sesión de Keycloak (para entrar con otro usuario)', async () => {
    const vista = await pintar();

    [...vista.querySelectorAll('button')]
      .find((b) => b.textContent?.includes('Cerrar sesión'))
      ?.click();

    expect(auth.logoutCalls).toBe(1);
  });
});
