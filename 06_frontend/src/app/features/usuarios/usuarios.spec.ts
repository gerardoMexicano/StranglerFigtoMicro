import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Component, input } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { AuthService } from '../../core/auth/auth.service';
import { FakeAuthService } from '../../core/auth/testing/fake-auth.service';
import { APP_CONFIG, AppConfig } from '../../core/config/app-config';
import { CheckIn } from './asistencia-api.service';
import { Geolocalizacion, GeolocalizacionError, Ubicacion } from './geolocalizacion';
import { MapaAsistencia } from './mapa-asistencia';
import { NotificacionNativa } from './notificacion-nativa';
import { Usuarios } from './usuarios';

const config: AppConfig = {
  oidc: { authority: 'http://kc/auth/realms/lab', clientId: 'frontend-spa', scope: 'openid' },
  apiBaseUrl: '/api',
  apps: { taxesUrl: '/taxes/Home/Menu', legacyUrl: '/legacy/Menu' },
};

const AQUI: Ubicacion = { latitud: 19.432608, longitud: -99.133209, precisionMetros: 12.5 };

const registro: CheckIn = {
  id: 7,
  latitude: 19.432608,
  longitude: -99.133209,
  accuracyMeters: 12.5,
  registeredAt: '2026-10-28T09:15:00-06:00',
};

/** Leaflet necesita un navegador de verdad: en las pruebas basta con ver qué recibe el mapa. */
@Component({ selector: 'app-mapa-asistencia', template: '' })
class MapaFalso {
  readonly ubicacion = input<Ubicacion | null>(null);
}

class GeolocalizacionFalsa {
  respuesta: Promise<Ubicacion> = Promise.resolve(AQUI);
  llamadas = 0;

  obtener(): Promise<Ubicacion> {
    this.llamadas++;
    return this.respuesta;
  }
}

class NotificacionFalsa {
  readonly avisos: string[] = [];

  async avisar(cuerpo: string): Promise<void> {
    this.avisos.push(cuerpo);
  }
}

describe('Usuarios', () => {
  let fixture: ComponentFixture<Usuarios>;
  let backend: HttpTestingController;
  let auth: FakeAuthService;
  let geo: GeolocalizacionFalsa;
  let notificacion: NotificacionFalsa;

  async function pintar(roles: string[]) {
    auth = new FakeAuthService();
    auth.session.next(true);
    auth.user.next({ userName: 'juan', name: 'Juan Pérez', roles });
    geo = new GeolocalizacionFalsa();
    notificacion = new NotificacionFalsa();

    TestBed.configureTestingModule({
      imports: [Usuarios],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: APP_CONFIG, useValue: config },
        { provide: AuthService, useValue: auth },
        { provide: Geolocalizacion, useValue: geo },
        { provide: NotificacionNativa, useValue: notificacion },
      ],
    });
    TestBed.overrideComponent(Usuarios, {
      remove: { imports: [MapaAsistencia] },
      add: { imports: [MapaFalso] },
    });
    backend = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(Usuarios);
    await asentar();
  }

  /** whenStable() esperaría a las peticiones pendientes; aquí solo corren promesas y la vista. */
  async function asentar() {
    await new Promise((resolve) => setTimeout(resolve));
    TestBed.tick();
  }

  afterEach(() => backend.verify());

  const vista = () => fixture.nativeElement as HTMLElement;
  const boton = () => vista().querySelector<HTMLButtonElement>('.acciones button')!;
  const aviso = () => vista().querySelector('.aviso')?.textContent?.trim();
  const mapa = () =>
    fixture.debugElement.query((d) => d.componentInstance instanceof MapaFalso)
      .componentInstance as MapaFalso;

  function historial(registros: CheckIn[]) {
    backend.expectOne((r) => r.url === '/api/attendance/check-ins/mine').flush(registros);
  }

  async function checar() {
    boton().click();
    await asentar();
  }

  it('al abrir pide el historial y lo muestra', async () => {
    await pintar(['usuario']);

    historial([registro]);
    await asentar();

    const filas = vista().querySelectorAll('tbody tr');
    expect(filas).toHaveLength(1);
    expect(filas[0].textContent).toContain('19.432608');
    expect(filas[0].textContent).toContain('-99.133209');
    expect(filas[0].textContent).toContain('± 13 m');
  });

  it('sin registros lo dice', async () => {
    await pintar(['usuario']);

    historial([]);
    await asentar();

    expect(vista().textContent).toContain('Todavía no tienes registros');
  });

  it('usuario: ubica, pinta el mapa, registra en el backend, recarga el historial y notifica', async () => {
    await pintar(['usuario']);
    historial([]);
    await asentar();
    expect(boton().textContent?.trim()).toBe('Registrar mi asistencia');

    await checar();
    const post = backend.expectOne('/api/attendance/check-ins');
    expect(post.request.method).toBe('POST');
    expect(post.request.body).toEqual({
      latitude: 19.432608,
      longitude: -99.133209,
      accuracyMeters: 12.5,
    });
    expect(mapa().ubicacion()).toEqual(AQUI);
    expect(boton().disabled).toBe(true);

    post.flush(registro, { status: 201, statusText: 'Created' });
    await asentar();
    historial([registro]);
    await asentar();

    expect(aviso()).toBe('Asistencia registrada en 19.432608, -99.133209');
    expect(notificacion.avisos).toEqual(['Asistencia registrada']);
    expect(vista().querySelectorAll('tbody tr')).toHaveLength(1);
    expect(boton().disabled).toBe(false);
    expect(boton().textContent?.trim()).toBe('Registrar de nuevo');
  });

  it('invitado: ve su ubicación en el mapa pero no registra', async () => {
    await pintar(['invitado']);
    historial([]);
    await asentar();
    expect(boton().textContent?.trim()).toBe('Obtener mi ubicación');
    expect(vista().textContent).toContain('la asistencia no se registra');

    await checar();

    backend.expectNone('/api/attendance/check-ins');
    expect(mapa().ubicacion()).toEqual(AQUI);
    expect(aviso()).toContain('Tu rol es de consulta');
    expect(notificacion.avisos).toEqual([]);
  });

  it('si el navegador no da la ubicación muestra el error y no llama al backend', async () => {
    await pintar(['admin']);
    historial([]);
    geo.respuesta = Promise.reject(new GeolocalizacionError('Permiso de ubicación denegado'));

    await checar();

    backend.expectNone('/api/attendance/check-ins');
    expect(vista().querySelector('[role="alert"]')?.textContent).toContain(
      'Permiso de ubicación denegado',
    );
    expect(mapa().ubicacion()).toBeNull();
    expect(boton().disabled).toBe(false);
  });

  it('si el backend rechaza el registro muestra su detalle', async () => {
    await pintar(['usuario']);
    historial([]);

    await checar();
    backend.expectOne('/api/attendance/check-ins').flush(
      {
        title: 'Solicitud inválida',
        detail: 'La latitud es obligatoria y debe estar entre -90 y 90.',
      },
      { status: 400, statusText: 'Bad Request' },
    );
    await asentar();

    expect(aviso()).toBe('La latitud es obligatoria y debe estar entre -90 y 90.');
    expect(notificacion.avisos).toEqual([]);
  });

  it('un segundo clic mientras ubica no hace otra petición', async () => {
    await pintar(['usuario']);
    historial([]);
    let ubicar!: (u: Ubicacion) => void;
    geo.respuesta = new Promise((resolve) => (ubicar = resolve));

    boton().click();
    boton().click();
    ubicar(AQUI);
    await asentar();

    expect(geo.llamadas).toBe(1);
    backend.expectOne('/api/attendance/check-ins').flush(registro);
    await asentar();
    historial([registro]);
  });

  it('tiene un enlace de regreso al menú', async () => {
    await pintar(['usuario']);
    historial([]);

    const volver = [...vista().querySelectorAll('a')].find((a) =>
      a.textContent?.includes('Volver'),
    );
    expect(volver?.getAttribute('href')).toBe('/menu');
  });
});
