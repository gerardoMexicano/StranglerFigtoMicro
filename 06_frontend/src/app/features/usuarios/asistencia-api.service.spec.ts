import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';

import { APP_CONFIG, AppConfig } from '../../core/config/app-config';
import { AsistenciaApiService, CheckIn, HISTORIAL_TAMANO } from './asistencia-api.service';

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
  return { service: TestBed.inject(AsistenciaApiService), backend: backendActual };
}

const registro: CheckIn = {
  id: 7,
  latitude: 19.432608,
  longitude: -99.133209,
  accuracyMeters: 12.5,
  registeredAt: '2026-10-28T09:15:00-06:00',
};

describe('AsistenciaApiService', () => {
  afterEach(() => {
    backendActual?.verify();
    backendActual = undefined;
  });

  it('POST /api/attendance/check-ins con la ubicación en el contrato del backend', async () => {
    const { service, backend } = configurar('/api');

    const respuesta = firstValueFrom(
      service.registrar({ latitud: 19.432608, longitud: -99.133209, precisionMetros: 12.5 }),
    );
    const req = backend.expectOne('/api/attendance/check-ins');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({
      latitude: 19.432608,
      longitude: -99.133209,
      accuracyMeters: 12.5,
    });
    req.flush(registro, { status: 201, statusText: 'Created' });

    expect(await respuesta).toEqual(registro);
  });

  it('GET /api/attendance/check-ins/mine con los últimos registros por defecto', async () => {
    const { service, backend } = configurar('/api');

    const respuesta = firstValueFrom(service.misRegistros());
    const req = backend.expectOne((r) => r.url === '/api/attendance/check-ins/mine');
    expect(req.request.method).toBe('GET');
    expect(req.request.params.get('take')).toBe(String(HISTORIAL_TAMANO));
    req.flush([registro]);

    expect(await respuesta).toEqual([registro]);
  });

  it('usa el apiBaseUrl de config.json y no duplica la diagonal', () => {
    const { service, backend } = configurar('http://192.168.50.10/api/');

    service.misRegistros(3).subscribe();

    const req = backend.expectOne(
      (r) => r.url === 'http://192.168.50.10/api/attendance/check-ins/mine',
    );
    expect(req.request.params.get('take')).toBe('3');
    req.flush([]);
  });

  it('propaga el 403 de invitado para que la vista lo explique', async () => {
    const { service, backend } = configurar('/api');

    const respuesta = firstValueFrom(
      service.registrar({ latitud: 19, longitud: -99, precisionMetros: 10 }),
    );
    backend
      .expectOne('/api/attendance/check-ins')
      .flush(null, { status: 403, statusText: 'Forbidden' });

    await expect(respuesta).rejects.toMatchObject({ status: 403 });
  });
});
