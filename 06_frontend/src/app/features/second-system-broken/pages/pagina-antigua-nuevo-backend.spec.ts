import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { APP_CONFIG, AppConfig } from '../../../core/config/app-config';
import {
  ConsolidatedReportItem,
  PagedResult,
} from '../consolidated-report-api.service';
import { PaginaAntiguaNuevoBackend } from './pagina-antigua-nuevo-backend';

const config: AppConfig = {
  oidc: { authority: 'http://kc/auth/realms/lab', clientId: 'frontend-spa', scope: 'openid' },
  apiBaseUrl: '/api',
  apps: { taxesUrl: '/taxes/Home/Menu', legacyUrl: '/legacy/Menu' },
};

const URL_REPORTE = '/api/taxes/consolidated-report';

const CONCEPTO_LARGO = 'Pago de nómina ordinaria correspondiente a la segunda quincena de marzo';

function fila(parcial: Partial<ConsolidatedReportItem> = {}): ConsolidatedReportItem {
  return {
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
    totalDeducciones: 3500.25,
    tipoMovimiento: 'Percepcion',
    concepto: CONCEPTO_LARGO,
    importe: 20000,
    ...parcial,
  };
}

function pagina(
  items: ConsolidatedReportItem[],
  page = 1,
  totalCount = items.length,
): PagedResult<ConsolidatedReportItem> {
  const totalPages = Math.ceil(totalCount / 50);
  return {
    items,
    page,
    pageSize: 50,
    totalCount,
    totalPages,
    hasPreviousPage: page > 1,
    hasNextPage: page < totalPages,
  };
}

describe('PaginaAntiguaNuevoBackend', () => {
  let fixture: ComponentFixture<PaginaAntiguaNuevoBackend>;
  let backend: HttpTestingController;

  beforeEach(async () => {
    TestBed.configureTestingModule({
      imports: [PaginaAntiguaNuevoBackend],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: APP_CONFIG, useValue: config },
      ],
    });
    backend = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(PaginaAntiguaNuevoBackend);
    await fixture.whenStable();
  });

  afterEach(() => backend.verify());

  const vista = () => fixture.nativeElement as HTMLElement;
  const texto = () => vista().textContent ?? '';
  const fecha = (nombre: string) =>
    vista().querySelector<HTMLInputElement>(`input[name="${nombre}"]`)!;
  const boton = (etiqueta: string) =>
    [...vista().querySelectorAll('button')].find((b) => b.textContent?.trim() === etiqueta)!;
  const celdas = () => [...vista().querySelectorAll('tbody tr:first-child td')];

  async function escribir(nombre: string, valor: string) {
    const input = fecha(nombre);
    input.value = valor;
    input.dispatchEvent(new Event('input'));
    await fixture.whenStable();
  }

  async function consultar(inicio = '2024-01-01', fin = '2024-03-31') {
    await escribir('fechaInicio', inicio);
    await escribir('fechaFin', fin);
    boton('Consultar').click();
    TestBed.tick();
  }

  async function responder(respuesta: PagedResult<ConsolidatedReportItem>) {
    backend.expectOne((r) => r.url === URL_REPORTE).flush(respuesta);
    await fixture.whenStable();
  }

  it('arranca sin fechas, sin pedir nada y con el aviso de la vista MVC', () => {
    TestBed.tick();

    expect(fecha('fechaInicio').value).toBe('');
    expect(fecha('fechaFin').value).toBe('');
    expect(backend.match(() => true)).toHaveLength(0);
    expect(texto()).toContain(
      'Por favor, utilice el filtro de fechas para consultar los registros.',
    );
  });

  it('indica que los datos vienen del backend nuevo', () => {
    expect(texto()).toContain('backend .NET 10 (/api/taxes/consolidated-report)');
  });

  it('"Consultar" pide la página 1 con las fechas escritas', async () => {
    await consultar('2024-01-01', '2024-03-31');

    const req = backend.expectOne((r) => r.url === URL_REPORTE);
    expect(req.request.params.get('startDate')).toBe('2024-01-01');
    expect(req.request.params.get('endDate')).toBe('2024-03-31');
    expect(req.request.params.get('page')).toBe('1');
    req.flush(pagina([]));
  });

  it('pinta las 14 columnas de la vista MVC en el mismo orden', async () => {
    await consultar();
    await responder(pagina([fila()]));

    const encabezados = [...vista().querySelectorAll('thead th')].map((th) =>
      th.textContent?.trim(),
    );
    expect(encabezados).toEqual([
      'UUID',
      'Emisor',
      'Receptor',
      'Total',
      'Fecha',
      'Complemento',
      'Num Emp.',
      'Depto.',
      'Puesto',
      'Tot. Perc.',
      'Tot. Deduc.',
      'Movimiento',
      'Concepto',
      'Importe',
    ]);
  });

  it('da formato de moneda y fecha dd/MM/yyyy como ToString("C") y ToString("dd/MM/yyyy")', async () => {
    await consultar();
    await responder(pagina([fila()]));

    const valores = celdas().map((td) => td.textContent?.trim());
    expect(valores[0]).toBe('9f1c2b7a-0000-4000-8000-000000000001');
    expect(valores[1]).toBe('AAA010101AAA');
    expect(valores[2]).toBe('BBB010101BBB');
    expect(valores[3]).toBe('$1,234.50');
    expect(valores[4]).toBe('15/03/2024');
    expect(valores[9]).toBe('$20,000.00');
    expect(valores[10]).toBe('$3,500.25');
    expect(valores[13]).toBe('$20,000.00');
  });

  it('corta el concepto a 40 caracteres con "..." y deja el texto completo en el title', async () => {
    await consultar();
    await responder(pagina([fila()]));

    const concepto = celdas()[12];
    expect(concepto.textContent?.trim()).toBe(`${CONCEPTO_LARGO.substring(0, 40)}...`);
    expect(concepto.getAttribute('title')).toBe(CONCEPTO_LARGO);
  });

  it('deja vacías las celdas de importes nulos en lugar de $0.00', async () => {
    await consultar();
    await responder(
      pagina([fila({ total: null, totalPercepciones: null, totalDeducciones: null, importe: null })]),
    );

    const valores = celdas().map((td) => td.textContent?.trim());
    expect([valores[3], valores[9], valores[10], valores[13]]).toEqual(['', '', '', '']);
  });

  it('resume cuántos registros muestra y en qué página va', async () => {
    await consultar();
    await responder(pagina([fila(), fila()], 1, 120));

    expect(texto()).toContain('Mostrando 2 de 120 registros (Página 1 de 3)');
  });

  it('"Siguiente" pide la página 2 con las mismas fechas y "Anterior" arranca deshabilitado', async () => {
    await consultar('2024-01-01', '2024-03-31');
    await responder(pagina([fila()], 1, 120));

    expect(boton('Anterior').disabled).toBe(true);
    boton('Siguiente').click();
    TestBed.tick();

    const req = backend.expectOne((r) => r.url === URL_REPORTE);
    expect(req.request.params.get('page')).toBe('2');
    expect(req.request.params.get('startDate')).toBe('2024-01-01');
    expect(req.request.params.get('endDate')).toBe('2024-03-31');
    req.flush(pagina([fila()], 2, 120));
  });

  it('en la última página deshabilita "Siguiente"', async () => {
    await consultar();
    await responder(pagina([fila()], 1, 1));

    expect(boton('Siguiente').disabled).toBe(true);
  });

  it('una consulta nueva regresa a la página 1', async () => {
    await consultar();
    await responder(pagina([fila()], 1, 120));
    boton('Siguiente').click();
    TestBed.tick();
    await responder(pagina([fila()], 2, 120));

    await consultar('2024-02-01', '2024-02-29');

    const req = backend.expectOne((r) => r.url === URL_REPORTE);
    expect(req.request.params.get('page')).toBe('1');
    expect(req.request.params.get('startDate')).toBe('2024-02-01');
    req.flush(pagina([]));
  });

  it('sin registros muestra el aviso de la vista MVC', async () => {
    await consultar();
    await responder(pagina([]));

    expect(texto()).toContain(
      'No se encontraron registros para el rango de fechas seleccionado.',
    );
  });

  it('con la fecha inicial después de la final avisa y no pide nada', async () => {
    await escribir('fechaInicio', '2024-05-01');
    await escribir('fechaFin', '2024-01-01');

    expect(boton('Consultar').disabled).toBe(true);
    expect(vista().querySelector('[role="alert"]')?.textContent).toContain('anterior o igual');
    boton('Consultar').click();
    TestBed.tick();
    expect(backend.match(() => true)).toHaveLength(0);
  });

  it('muestra el título del ProblemDetails cuando el backend responde 400', async () => {
    await consultar();
    backend
      .expectOne((r) => r.url === URL_REPORTE)
      .flush(
        { title: 'Validation', detail: 'La fecha de inicio no puede ser mayor que la fecha final.' },
        { status: 400, statusText: 'Bad Request' },
      );
    await fixture.whenStable();

    expect(vista().querySelector('[role="alert"]')?.textContent).toContain(
      'La fecha de inicio no puede ser mayor que la fecha final.',
    );
  });

  it('avisa cuando no hay conexión con el backend', async () => {
    await consultar();
    backend
      .expectOne((r) => r.url === URL_REPORTE)
      .error(new ProgressEvent('error'), { status: 0 });
    await fixture.whenStable();

    expect(vista().querySelector('[role="alert"]')?.textContent).toContain(
      'Sin conexión con el backend.',
    );
  });

  it('"Limpiar" borra fechas y resultados y vuelve al aviso inicial', async () => {
    await consultar();
    await responder(pagina([fila()]));

    boton('Limpiar').click();
    await fixture.whenStable();

    expect(fecha('fechaInicio').value).toBe('');
    expect(fecha('fechaFin').value).toBe('');
    expect(vista().querySelectorAll('tbody tr td')).toHaveLength(1);
    expect(texto()).toContain(
      'Por favor, utilice el filtro de fechas para consultar los registros.',
    );
  });

  it('tiene un enlace de regreso a Second System Broken', () => {
    const volver = [...vista().querySelectorAll('a')].find((a) =>
      a.textContent?.includes('Volver'),
    );

    expect(volver?.getAttribute('href')).toBe('/second-system-broken');
  });
});
