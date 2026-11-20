import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Component, input } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { provideRouter } from '@angular/router';

import { APP_CONFIG, AppConfig } from '../../../core/config/app-config';
import { PuntoSismico, ResultadoSismos } from '../sismos-api.service';
import { MapaSismos } from './mapa-sismos';
import { PaginaAngular } from './pagina-angular';

const config: AppConfig = {
  oidc: { authority: 'http://kc/auth/realms/lab', clientId: 'frontend-spa', scope: 'openid' },
  apiBaseUrl: '/api',
  apps: { taxesUrl: '/taxes/Home/Menu', legacyUrl: '/legacy/Menu' },
};

const URL_SISMOS = '/api/taxes/seismic-geojson';

/** Leaflet no pinta en jsdom: el mapa falso solo guarda los puntos que recibe. */
@Component({ selector: 'app-mapa-sismos', template: '', host: { 'data-mapa': 'falso' } })
class MapaFalso {
  readonly puntos = input<readonly PuntoSismico[]>([]);
}

function resultado(parcial: Partial<ResultadoSismos> = {}): ResultadoSismos {
  return {
    engine: '.NET 10 (Clean Architecture)',
    requestedCount: 5000,
    returnedCount: 3,
    minMagnitude: 1,
    seed: 2026,
    averageMagnitude: 3.42,
    maxMagnitude: 5.98,
    serverElapsedMs: 12,
    memoryMegabytes: 0.35,
    points: [
      { lat: 19.43, lng: -99.13, mag: 2.1 },
      { lat: 16.85, lng: -99.88, mag: 5.98 },
      { lat: 20.67, lng: -103.35, mag: 2.18 },
    ],
    ...parcial,
  };
}

describe('PaginaAngular (comparativa de sismos en .NET 10)', () => {
  let fixture: ComponentFixture<PaginaAngular>;
  let backend: HttpTestingController;

  beforeEach(async () => {
    TestBed.configureTestingModule({
      imports: [PaginaAngular],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: APP_CONFIG, useValue: config },
      ],
    });
    TestBed.overrideComponent(PaginaAngular, {
      remove: { imports: [MapaSismos] },
      add: { imports: [MapaFalso] },
    });
    backend = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(PaginaAngular);
    await fixture.whenStable();
  });

  afterEach(() => backend.verify());

  const vista = () => fixture.nativeElement as HTMLElement;
  const texto = () => vista().textContent ?? '';
  const campo = (nombre: string) =>
    vista().querySelector<HTMLInputElement>(`input[name="${nombre}"]`)!;
  const ejecutar = () =>
    [...vista().querySelectorAll('button')].find((b) => b.textContent?.trim() === 'Ejecutar')!;
  const mapa = () => fixture.debugElement.query(By.directive(MapaFalso)).componentInstance as MapaFalso;
  const alerta = () => vista().querySelector('[role="alert"]')?.textContent ?? '';

  async function escribir(nombre: string, valor: string) {
    const input = campo(nombre);
    input.value = valor;
    input.dispatchEvent(new Event('input'));
    await fixture.whenStable();
  }

  function correr() {
    ejecutar().click();
    TestBed.tick();
    return backend.expectOne((r) => r.url === URL_SISMOS);
  }

  it('arranca con los valores de la vista MVC, sin pedir nada y con el mapa vacío', () => {
    TestBed.tick();

    expect(campo('count').value).toBe('5000');
    expect(campo('minMagnitude').value).toBe('1');
    expect(campo('seed').value).toBe('2026');
    expect(backend.match(() => true)).toHaveLength(0);
    expect(mapa().puntos()).toEqual([]);
    expect(texto()).toContain('Esperando ejecución');
  });

  it('indica que solo consulta el backend .NET 10', () => {
    expect(texto()).toContain('/api/taxes/seismic-geojson');
  });

  it('"Ejecutar" pide los sismos con los parámetros escritos', async () => {
    await escribir('count', '2500');
    await escribir('minMagnitude', '3.5');
    await escribir('seed', '7');

    const req = correr();

    expect(req.request.params.get('count')).toBe('2500');
    expect(req.request.params.get('minMagnitude')).toBe('3.5');
    expect(req.request.params.get('seed')).toBe('7');
    req.flush(resultado());
  });

  it('deshabilita "Ejecutar" mientras espera la respuesta', async () => {
    const req = correr();
    await fixture.whenStable().catch(() => undefined);
    fixture.detectChanges();

    expect(ejecutar().disabled).toBe(true);
    req.flush(resultado());
    await fixture.whenStable();
    expect(ejecutar().disabled).toBe(false);
  });

  it('muestra las métricas del backend: motor, estado HTTP, sismos, promedio, máxima, servidor y memoria', async () => {
    correr().flush(resultado());
    await fixture.whenStable();

    expect(texto()).toContain('.NET 10 (Clean Architecture)');
    expect(texto()).toContain('HTTP 200');
    expect(texto()).toContain('3 pts (Ø 3.42)');
    expect(texto()).toContain('5.98');
    expect(texto()).toContain('Srv: 12 ms');
    expect(texto()).toContain('0.35 MB');
    expect(texto()).not.toContain('Esperando ejecución');
  });

  it('manda los puntos al mapa', async () => {
    const respuesta = resultado();
    correr().flush(respuesta);
    await fixture.whenStable();

    expect(mapa().puntos()).toEqual(respuesta.points);
  });

  it('con un 400 muestra el detalle de la validación y limpia el mapa', async () => {
    correr().flush(resultado());
    await fixture.whenStable();

    await escribir('count', '20000');
    correr().flush(
      {
        title: 'Validation',
        detail: 'La cantidad de sismos debe estar entre 1 y 10000.',
      },
      { status: 400, statusText: 'Bad Request' },
    );
    await fixture.whenStable();

    expect(alerta()).toContain('HTTP 400');
    expect(alerta()).toContain('La cantidad de sismos debe estar entre 1 y 10000.');
    expect(mapa().puntos()).toEqual([]);
  });

  it('sin conexión avisa que no pudo conectar con el servidor', async () => {
    correr().error(new ProgressEvent('error'), { status: 0 });
    await fixture.whenStable();

    expect(alerta()).toContain('No se pudo conectar con el servidor.');
  });

  it('con 403 avisa que el rol no tiene acceso', async () => {
    correr().flush({ title: 'Forbidden' }, { status: 403, statusText: 'Forbidden' });
    await fixture.whenStable();

    expect(alerta()).toContain('HTTP 403');
  });

  it('tiene un enlace de regreso a Second System Broken', () => {
    const volver = [...vista().querySelectorAll('a')].find((a) =>
      a.textContent?.includes('Volver'),
    );

    expect(volver?.getAttribute('href')).toBe('/second-system-broken');
  });
});
