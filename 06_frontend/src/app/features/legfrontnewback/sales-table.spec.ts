import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';

import { APP_CONFIG, AppConfig } from '../../core/config/app-config';
import { PagedResult, SalesRow } from './sales-api.service';
import { RangoFechas, SalesTable } from './sales-table';

const config: AppConfig = {
  oidc: { authority: 'http://kc/auth/realms/lab', clientId: 'frontend-spa', scope: 'openid' },
  apiBaseUrl: '/api',
  apps: { taxesUrl: '/taxes/Home/Menu', legacyUrl: '/legacy/Menu' },
};

const rango: RangoFechas = { startDate: '2013-01-01', endDate: '2016-12-31' };

function pagina(page: number, items: SalesRow[], totalPages = 3): PagedResult<SalesRow> {
  return {
    items,
    page,
    pageSize: 50,
    totalCount: totalPages * 50,
    totalPages,
    hasPreviousPage: page > 1,
    hasNextPage: page < totalPages,
  };
}

const filas: SalesRow[] = [
  { orderID: 73595, customerID: 832, comments: null },
  { orderID: 73594, customerID: 1047, comments: 'urgente' },
];

describe('SalesTable', () => {
  let fixture: ComponentFixture<SalesTable>;
  let backend: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [SalesTable],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: APP_CONFIG, useValue: config },
      ],
    });
    backend = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(SalesTable);
    fixture.componentRef.setInput('resource', 'orders');
    fixture.componentRef.setInput('title', 'Orders');
  });

  afterEach(() => backend.verify());

  const vista = () => fixture.nativeElement as HTMLElement;
  const texto = () => vista().textContent?.replace(/\s+/g, ' ') ?? '';

  /** Corre los efectos (el resource arma la petición) y devuelve la petición pendiente. */
  function peticion(page: number) {
    fixture.detectChanges();
    TestBed.tick();
    return backend.expectOne(
      (r) => r.url === '/api/sales/orders' && r.params.get('page') === String(page),
    );
  }

  async function responder(page: number, cuerpo: PagedResult<SalesRow>) {
    peticion(page).flush(cuerpo);
    await fixture.whenStable();
  }

  function boton(nombre: string): HTMLButtonElement {
    return [...vista().querySelectorAll('button')].find((b) => b.textContent?.trim() === nombre)!;
  }

  it('sin rango no pide nada', async () => {
    await fixture.whenStable();

    backend.expectNone(() => true);
    expect(texto()).toContain('Sin cargar');
  });

  it('con rango pide la página 1 y mientras tanto dice que está cargando', () => {
    fixture.componentRef.setInput('rango', rango);

    const req = peticion(1);
    fixture.detectChanges();

    expect(req.request.params.get('startDate')).toBe('2013-01-01');
    expect(req.request.params.get('endDate')).toBe('2016-12-31');
    expect(texto()).toContain('Cargando');
    req.flush(pagina(1, filas));
  });

  it('las columnas salen de las propiedades de las filas, como AutoGenerateColumns', async () => {
    fixture.componentRef.setInput('rango', rango);
    await responder(1, pagina(1, filas));

    const encabezados = [...vista().querySelectorAll('th')].map((th) => th.textContent?.trim());
    expect(encabezados).toEqual(['orderID', 'customerID', 'comments']);
    expect(vista().querySelectorAll('tbody tr')).toHaveLength(2);
    expect(vista().querySelector('tbody tr td:last-child')?.textContent).toBe('');
  });

  it('muestra el total, la página y cuánto tardó', async () => {
    fixture.componentRef.setInput('rango', rango);
    await responder(1, pagina(1, filas, 1472));

    expect(texto()).toContain('73,600 registros');
    expect(texto()).toMatch(/\d+ ms/);
    expect(texto()).toContain('Página 1 de 1,472');
  });

  it('"Siguiente" y "Anterior" piden otra página al servidor', async () => {
    fixture.componentRef.setInput('rango', rango);
    await responder(1, pagina(1, filas));
    expect(boton('Anterior').disabled).toBe(true);

    boton('Siguiente').click();
    await responder(2, pagina(2, filas));
    expect(texto()).toContain('Página 2 de 3');

    boton('Anterior').click();
    await responder(1, pagina(1, filas));
    expect(texto()).toContain('Página 1 de 3');
  });

  it('en la última página "Siguiente" queda deshabilitado', async () => {
    fixture.componentRef.setInput('rango', rango);
    await responder(1, pagina(1, filas, 1));

    expect(boton('Siguiente').disabled).toBe(true);
  });

  it('un rango nuevo vuelve a la página 1', async () => {
    fixture.componentRef.setInput('rango', rango);
    await responder(1, pagina(1, filas));
    boton('Siguiente').click();
    await responder(2, pagina(2, filas));

    fixture.componentRef.setInput('rango', { startDate: '2015-01-01', endDate: '2015-12-31' });
    const req = peticion(1);

    expect(req.request.params.get('startDate')).toBe('2015-01-01');
    req.flush(pagina(1, filas));
  });

  it('sin filas lo dice en lugar de pintar una tabla vacía', async () => {
    fixture.componentRef.setInput('rango', rango);
    await responder(1, pagina(1, [], 0));

    expect(vista().querySelector('table')).toBeNull();
    expect(texto()).toContain('Sin registros en el rango');
  });

  it.each([
    [502, null, 'El backend no está disponible (502).'],
    [400, { title: 'Rango de fechas inválido' }, 'Rango de fechas inválido (400).'],
    [500, null, 'El backend respondió 500.'],
  ])('si el backend responde %i lo muestra', async (status, cuerpo, mensaje) => {
    fixture.componentRef.setInput('rango', rango);

    peticion(1).flush(cuerpo, { status, statusText: 'Error' });
    await fixture.whenStable();

    expect(vista().querySelector('.error')?.textContent?.trim()).toBe(mensaje);
  });
});
