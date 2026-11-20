import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';

import { APP_CONFIG, AppConfig } from '../../core/config/app-config';
import {
  DashboardApiService,
  DashboardEjecutivo,
  RANGO_DASHBOARD_POR_DEFECTO,
} from './dashboard-api.service';

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
  return { service: TestBed.inject(DashboardApiService), backend: backendActual };
}

describe('DashboardApiService', () => {
  afterEach(() => {
    backendActual?.verify();
    backendActual = undefined;
  });

  it('por defecto usa el último tramo con datos de WideWorldImporters', () => {
    expect(RANGO_DASHBOARD_POR_DEFECTO).toEqual({
      startDate: '2016-01-01',
      endDate: '2016-05-31',
    });
  });

  it('GET /api/dashboard/executive con el rango de fechas', () => {
    const { service, backend } = configurar('/api');

    service.consultar({ startDate: '2015-01-01', endDate: '2015-12-31' }).subscribe();

    const req = backend.expectOne((r) => r.url === '/api/dashboard/executive');
    expect(req.request.method).toBe('GET');
    expect(req.request.params.get('startDate')).toBe('2015-01-01');
    expect(req.request.params.get('endDate')).toBe('2015-12-31');
    req.flush({});
  });

  it('devuelve el dashboard tal como lo manda el backend', async () => {
    const { service, backend } = configurar('/api');
    const dashboard: DashboardEjecutivo = {
      startDate: '2016-01-01T00:00:00',
      endDate: '2016-05-31T00:00:00',
      kpis: {
        current: { revenue: 1000, grossMarginPct: 45.5, orders: 10, averageTicket: 100 },
        previous: { revenue: 800, grossMarginPct: 44, orders: 8, averageTicket: 100 },
        previousStartDate: '2015-08-03T00:00:00',
        previousEndDate: '2015-12-31T00:00:00',
      },
      monthlyRevenue: [],
      topCustomers: [],
      categoryMargins: [],
      salespeople: [],
    };

    const respuesta = firstValueFrom(service.consultar(RANGO_DASHBOARD_POR_DEFECTO));
    backend.expectOne((r) => r.url === '/api/dashboard/executive').flush(dashboard);

    expect(await respuesta).toEqual(dashboard);
  });

  it('usa el apiBaseUrl de config.json y no duplica la diagonal', () => {
    const { service, backend } = configurar('http://192.168.50.10/api/');

    service.consultar(RANGO_DASHBOARD_POR_DEFECTO).subscribe();

    backend
      .expectOne((r) => r.url === 'http://192.168.50.10/api/dashboard/executive')
      .flush({});
  });

  it('propaga el 400 de validación del backend', async () => {
    const { service, backend } = configurar('/api');

    const respuesta = firstValueFrom(
      service.consultar({ startDate: '2016-05-31', endDate: '2016-01-01' }),
    );
    backend
      .expectOne((r) => r.url === '/api/dashboard/executive')
      .flush({ title: 'Validation' }, { status: 400, statusText: 'Bad Request' });

    await expect(respuesta).rejects.toMatchObject({ status: 400 });
  });
});
