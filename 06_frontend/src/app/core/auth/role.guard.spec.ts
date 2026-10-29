import { TestBed } from '@angular/core/testing';
import {
  ActivatedRouteSnapshot,
  provideRouter,
  Router,
  RouterStateSnapshot,
  UrlTree,
} from '@angular/router';
import { firstValueFrom, isObservable, Observable } from 'rxjs';

import { AuthService } from './auth.service';
import { roleGuard } from './role.guard';
import { FakeAuthService } from './testing/fake-auth.service';

describe('roleGuard', () => {
  let auth: FakeAuthService;

  beforeEach(() => {
    auth = new FakeAuthService();
    TestBed.configureTestingModule({
      providers: [provideRouter([]), { provide: AuthService, useValue: auth }],
    });
  });

  async function activar(roles?: string[]): Promise<unknown> {
    const ruta = { data: roles ? { roles } : {} } as unknown as ActivatedRouteSnapshot;
    const resultado = TestBed.runInInjectionContext(() =>
      roleGuard(ruta, { url: '/x' } as RouterStateSnapshot),
    );
    return isObservable(resultado) ? firstValueFrom(resultado as Observable<unknown>) : resultado;
  }

  function usuarioCon(roles: string[]): void {
    auth.session.next(true);
    auth.user.next({ userName: 'juan', name: 'Juan', roles });
  }

  it('deja pasar a quien tiene alguno de los roles de la ruta', async () => {
    usuarioCon(['invitado']);

    expect(await activar(['usuario', 'invitado'])).toBe(true);
  });

  it('sin ninguno de los roles manda a /sin-acceso', async () => {
    usuarioCon(['otro']);

    const resultado = await activar(['usuario', 'admin']);

    expect(resultado).toBeInstanceOf(UrlTree);
    expect(TestBed.inject(Router).serializeUrl(resultado as UrlTree)).toBe('/sin-acceso');
  });

  it('una ruta sin roles la ve cualquiera con sesión', async () => {
    usuarioCon([]);

    expect(await activar()).toBe(true);
  });

  it('sin sesión no deja pasar ni redirige: el login lo resuelve authGuard', async () => {
    auth.user.next(null);

    expect(await activar(['usuario'])).toBe(false);
  });
});
