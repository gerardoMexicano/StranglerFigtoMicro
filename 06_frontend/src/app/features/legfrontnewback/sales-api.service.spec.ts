import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';

import { APP_CONFIG, AppConfig } from '../../core/config/app-config';
import {
  PagedResult,
  SALES_PAGE_SIZE,
  SALES_RESOURCES,
  SalesApiService,
  SalesQuery,
  SalesRow,
} from './sales-api.service';

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
  return { service: TestBed.inject(SalesApiService), backend: backendActual };
}

// El rango por defecto de LEgFrontNEWBACK.aspx
const consulta: SalesQuery = { startDate: '2013-01-01', endDate: '2016-12-31', page: 3 };

describe('SalesApiService', () => {
  afterEach(() => {
    backendActual?.verify();
    backendActual = undefined;
  });

  it('pagina de 50 en 50, igual que el GridView de WebForms', () => {
    expect(SALES_PAGE_SIZE).toBe(50);
  });

  it('cubre las cinco tablas de LEgFrontNEWBACK', () => {
    expect([...SALES_RESOURCES]).toEqual([
      'customer-transactions',
      'invoice-lines',
      'invoices',
      'order-lines',
      'orders',
    ]);
  });

  it.each(SALES_RESOURCES)('GET /api/sales/%s con fechas, página y tamaño', (recurso) => {
    const { service, backend } = configurar('/api');

    service.getPage(recurso, consulta).subscribe();

    const req = backend.expectOne((r) => r.url === `/api/sales/${recurso}`);
    expect(req.request.method).toBe('GET');
    expect(req.request.params.get('startDate')).toBe('2013-01-01');
    expect(req.request.params.get('endDate')).toBe('2016-12-31');
    expect(req.request.params.get('page')).toBe('3');
    expect(req.request.params.get('pageSize')).toBe('50');
    req.flush({
      items: [],
      page: 3,
      pageSize: 50,
      totalCount: 0,
      totalPages: 0,
      hasPreviousPage: true,
      hasNextPage: false,
    });
  });

  it('devuelve la página tal como la manda el backend', async () => {
    const { service, backend } = configurar('/api');
    const pagina: PagedResult<SalesRow> = {
      items: [
        { orderID: 73595, customerID: 832, orderDate: '2016-05-31T00:00:00' },
        { orderID: 73594, customerID: 1047, orderDate: '2016-05-31T00:00:00' },
      ],
      page: 3,
      pageSize: 50,
      totalCount: 73595,
      totalPages: 1472,
      hasPreviousPage: true,
      hasNextPage: true,
    };

    const respuesta = firstValueFrom(service.getPage('orders', consulta));
    backend.expectOne((r) => r.url === '/api/sales/orders').flush(pagina);

    expect(await respuesta).toEqual(pagina);
  });

  it('usa el apiBaseUrl de config.json y no duplica la diagonal', () => {
    const { service, backend } = configurar('http://192.168.50.10/api/');

    service.getPage('invoices', consulta).subscribe();

    backend.expectOne((r) => r.url === 'http://192.168.50.10/api/sales/invoices').flush({});
  });

  it('propaga los errores del backend (400 de validación) para que la vista los muestre', async () => {
    const { service, backend } = configurar('/api');

    const respuesta = firstValueFrom(
      service.getPage('orders', { ...consulta, startDate: '2017-01-01' }),
    );
    backend
      .expectOne((r) => r.url === '/api/sales/orders')
      .flush({ title: 'Validation' }, { status: 400, statusText: 'Bad Request' });

    await expect(respuesta).rejects.toMatchObject({ status: 400 });
  });
});
