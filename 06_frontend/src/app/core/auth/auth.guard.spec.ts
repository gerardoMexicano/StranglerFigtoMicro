import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, RouterStateSnapshot } from '@angular/router';
import { firstValueFrom, isObservable, Observable } from 'rxjs';

import { routes } from '../../app.routes';
import { authGuard } from './auth.guard';
import { AuthService } from './auth.service';
import { FakeAuthService } from './testing/fake-auth.service';

describe('authGuard', () => {
  let auth: FakeAuthService;

  beforeEach(() => {
    auth = new FakeAuthService();
    TestBed.configureTestingModule({
      providers: [{ provide: AuthService, useValue: auth }],
    });
  });

  async function activar(url: string): Promise<unknown> {
    const resultado = TestBed.runInInjectionContext(() =>
      authGuard({} as ActivatedRouteSnapshot, { url } as RouterStateSnapshot),
    );
    return isObservable(resultado) ? firstValueFrom(resultado as Observable<unknown>) : resultado;
  }

  it('con sesión deja pasar y no manda a Keycloak', async () => {
    auth.session.next(true);

    expect(await activar('/menu')).toBe(true);
    expect(auth.loginCalls).toEqual([]);
  });

  it('sin sesión no deja pasar y manda a Keycloak recordando la ruta completa', async () => {
    auth.session.next(false);

    expect(await activar('/legfrontnewback?pagina=3')).toBe(false);
    expect(auth.loginCalls).toEqual(['/legfrontnewback?pagina=3']);
  });
});

describe('rutas de /newapp', () => {
  function ruta(path: string) {
    return routes.find((r) => r.path === path);
  }

  it('la presentación ("") es pública', () => {
    expect(ruta('')).toBeDefined();
    expect(ruta('')?.canActivate ?? []).not.toContain(authGuard);
  });

  it.each(['menu', 'legfrontnewback'])('"%s" exige sesión', (path) => {
    expect(ruta(path)).toBeDefined();
    expect(ruta(path)?.canActivate).toContain(authGuard);
  });

  it('una ruta desconocida regresa a la presentación', () => {
    expect(ruta('**')?.redirectTo).toBe('');
  });
});
