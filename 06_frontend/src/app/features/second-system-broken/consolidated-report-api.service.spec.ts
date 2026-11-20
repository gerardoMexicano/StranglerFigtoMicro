import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';

import { APP_CONFIG, AppConfig } from '../../core/config/app-config';
import {
  CONSOLIDATED_REPORT_PAGE_SIZE,
  ConsolidatedReportApiService,
  ConsolidatedReportItem,
  ConsolidatedReportQuery,
  PagedResult,
} from './consolidated-report-api.service';

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
  return { service: TestBed.inject(ConsolidatedReportApiService), backend: backendActual };
}

const consulta: ConsolidatedReportQuery = {
  startDate: '2024-01-01',
  endDate: '2024-03-31',
  page: 2,
};

describe('ConsolidatedReportApiService', () => {
  afterEach(() => {
    backendActual?.verify();
    backendActual = undefined;
  });

  it('pagina de 50 en 50, igual que la vista MVC', () => {
    expect(CONSOLIDATED_REPORT_PAGE_SIZE).toBe(50);
  });

  it('GET /api/taxes/consolidated-report con fechas, página y tamaño', () => {
    const { service, backend } = configurar('/api');

    service.getPage(consulta).subscribe();

    const req = backend.expectOne((r) => r.url === '/api/taxes/consolidated-report');
    expect(req.request.method).toBe('GET');
    expect(req.request.params.get('startDate')).toBe('2024-01-01');
    expect(req.request.params.get('endDate')).toBe('2024-03-31');
    expect(req.request.params.get('page')).toBe('2');
    expect(req.request.params.get('pageSize')).toBe('50');
    req.flush({ items: [], page: 2, pageSize: 50, totalCount: 0, totalPages: 0 });
  });

  it('devuelve la página tal como la manda el backend', async () => {
    const { service, backend } = configurar('/api');
    const pagina: PagedResult<ConsolidatedReportItem> = {
      items: [
        {
          uuid: '9f1c2b7a-0000-4000-8000-000000000001',
          emisorRFC: 'AAA010101AAA',
          receptorRFC: 'BBB010101BBB',
          total: 1234.5,
          fecha: '2024-03-15T10:20:00',
          tipoComplemento: 'Nomina',
          numEmpleado: '0042',
          departamento: 'Sistemas',
          puesto: 'Desarrollador',
          totalPercepciones: 20000,
          totalDeducciones: 3500,
          tipoMovimiento: 'Percepcion',
          concepto: 'Sueldo',
          importe: 20000,
        },
      ],
      page: 2,
      pageSize: 50,
      totalCount: 51,
      totalPages: 2,
      hasPreviousPage: true,
      hasNextPage: false,
    };

    const respuesta = firstValueFrom(service.getPage(consulta));
    backend.expectOne((r) => r.url === '/api/taxes/consolidated-report').flush(pagina);

    expect(await respuesta).toEqual(pagina);
  });

  it('usa el apiBaseUrl de config.json y no duplica la diagonal', () => {
    const { service, backend } = configurar('http://192.168.50.10/api/');

    service.getPage(consulta).subscribe();

    backend
      .expectOne((r) => r.url === 'http://192.168.50.10/api/taxes/consolidated-report')
      .flush({});
  });

  it('propaga los errores del backend (400 de validación) para que la vista los muestre', async () => {
    const { service, backend } = configurar('/api');

    const respuesta = firstValueFrom(service.getPage({ ...consulta, startDate: '2025-01-01' }));
    backend
      .expectOne((r) => r.url === '/api/taxes/consolidated-report')
      .flush({ title: 'Validation' }, { status: 400, statusText: 'Bad Request' });

    await expect(respuesta).rejects.toMatchObject({ status: 400 });
  });
});
