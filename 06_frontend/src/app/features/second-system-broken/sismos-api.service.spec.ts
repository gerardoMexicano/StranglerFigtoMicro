import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';

import { APP_CONFIG, AppConfig } from '../../core/config/app-config';
import {
  CONSULTA_SISMOS_POR_DEFECTO,
  ResultadoSismos,
  SismosApiService,
} from './sismos-api.service';

let backendActual: HttpTestingController | undefined;

function configurar(apiBaseUrl: string) {
  const config: AppConfig = {
    oidc: { authority: 'http://kc/auth/realms/lab', clientId: 'frontend-spa', scope: 'openid' },
    apiBaseUrl,
    apps: { taxesUrl: '/taxes/Home/Menu', legacyUrl: '/legacy/Menu' },
  };
  TestBed.configureTestingModule({
    providers: [
      provideHttpClient(),
      provideHttpClientTesting(),
      { provide: APP_CONFIG, useValue: config },
    ],
  });
  backendActual = TestBed.inject(HttpTestingController);
  return { service: TestBed.inject(SismosApiService), backend: backendActual };
}

describe('SismosApiService', () => {
  afterEach(() => {
    backendActual?.verify();
    backendActual = undefined;
  });

  it('usa los valores por defecto de la vista MVC: 5000 sismos, magnitud 1.0, semilla 2026', () => {
    expect(CONSULTA_SISMOS_POR_DEFECTO).toEqual({ count: 5000, minMagnitude: 1.0, seed: 2026 });
  });

  it('GET /api/taxes/seismic-geojson con cantidad, magnitud mínima y semilla', () => {
    const { service, backend } = configurar('/api');

    service.consultar({ count: 2500, minMagnitude: 3.5, seed: 7 }).subscribe();

    const req = backend.expectOne((r) => r.url === '/api/taxes/seismic-geojson');
    expect(req.request.method).toBe('GET');
    expect(req.request.params.get('count')).toBe('2500');
    expect(req.request.params.get('minMagnitude')).toBe('3.5');
    expect(req.request.params.get('seed')).toBe('7');
    req.flush({});
  });

  it('devuelve el resultado tal como lo manda el backend', async () => {
    const { service, backend } = configurar('/api');
    const resultado: ResultadoSismos = {
      engine: '.NET 10 (Clean Architecture)',
      requestedCount: 3,
      returnedCount: 2,
      minMagnitude: 1,
      seed: 2026,
      averageMagnitude: 3.25,
      maxMagnitude: 4.5,
      serverElapsedMs: 4,
      memoryMegabytes: 0.02,
      points: [
        { lat: 19.43, lng: -99.13, mag: 2 },
        { lat: 16.85, lng: -99.88, mag: 4.5 },
      ],
    };

    const respuesta = firstValueFrom(service.consultar(CONSULTA_SISMOS_POR_DEFECTO));
    backend.expectOne((r) => r.url === '/api/taxes/seismic-geojson').flush(resultado);

    expect(await respuesta).toEqual(resultado);
  });

  it('usa el apiBaseUrl de config.json y no duplica la diagonal', () => {
    const { service, backend } = configurar('http://192.168.50.10/api/');

    service.consultar(CONSULTA_SISMOS_POR_DEFECTO).subscribe();

    backend
      .expectOne((r) => r.url === 'http://192.168.50.10/api/taxes/seismic-geojson')
      .flush({});
  });

  it('propaga el 400 de validación del backend para que la vista lo muestre', async () => {
    const { service, backend } = configurar('/api');

    const respuesta = firstValueFrom(
      service.consultar({ ...CONSULTA_SISMOS_POR_DEFECTO, count: 20000 }),
    );
    backend
      .expectOne((r) => r.url === '/api/taxes/seismic-geojson')
      .flush({ title: 'Validation' }, { status: 400, statusText: 'Bad Request' });

    await expect(respuesta).rejects.toMatchObject({ status: 400 });
  });
});
