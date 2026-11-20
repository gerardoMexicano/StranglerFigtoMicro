import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { APP_CONFIG, AppConfig } from '../../../core/config/app-config';
import { DashboardEjecutivo } from '../dashboard-api.service';
import { Dashboard } from './dashboard';

const config: AppConfig = {
  oidc: { authority: 'http://kc/auth/realms/lab', clientId: 'frontend-spa', scope: 'openid' },
  apiBaseUrl: '/api',
  apps: { taxesUrl: '/taxes/Home/Menu', legacyUrl: '/legacy/Menu' },
};

const URL_DASHBOARD = '/api/dashboard/executive';

function dashboard(parcial: Partial<DashboardEjecutivo> = {}): DashboardEjecutivo {
  return {
    startDate: '2016-01-01T00:00:00',
    endDate: '2016-05-31T00:00:00',
    kpis: {
      current: { revenue: 1_000_000, grossMarginPct: 45.5, orders: 1200, averageTicket: 833.33 },
      previous: { revenue: 800_000, grossMarginPct: 44.1, orders: 1500, averageTicket: 533.33 },
      previousStartDate: '2015-08-03T00:00:00',
      previousEndDate: '2015-12-31T00:00:00',
    },
    monthlyRevenue: [
      { year: 2016, month: 1, grossRevenue: 4_000_000, taxes: 600_000, totalRevenue: 4_600_000 },
      { year: 2016, month: 2, grossRevenue: 3_500_000, taxes: 525_000, totalRevenue: 4_025_000 },
    ],
    topCustomers: [
      { customer: 'Tailspin Toys (Head Office)', netMargin: 120_000 },
      { customer: 'Wingtip Toys (Head Office)', netMargin: 110_000 },
    ],
    categoryMargins: [
      { category: 'Grupo 2', grossMargin: 300_000 },
      { category: 'Grupo 7', grossMargin: 150_000 },
    ],
    salespeople: [
      { salesperson: 'Kayla Woodcock', revenue: 1_250_000, marginPct: 42.1, lowMargin: false },
      { salesperson: 'Hudson Onslow', revenue: 500_000, marginPct: 12.5, lowMargin: true },
    ],
    ...parcial,
  };
}

describe('Dashboard ejecutivo', () => {
  let fixture: ComponentFixture<Dashboard>;
  let backend: HttpTestingController;

  beforeEach(async () => {
    TestBed.configureTestingModule({
      imports: [Dashboard],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: APP_CONFIG, useValue: config },
      ],
    });
    backend = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(Dashboard);
    await fixture.whenStable().catch(() => undefined);
    TestBed.tick();
  });

  afterEach(() => backend.verify());

  const vista = () => fixture.nativeElement as HTMLElement;
  const seccion = (nombre: string) =>
    vista().querySelector<HTMLElement>(`section[aria-label="${nombre}"]`)!;
  const kpi = (id: string) => vista().querySelector<HTMLElement>(`[data-kpi="${id}"]`)!;
  const valorKpi = (id: string) => kpi(id).querySelector('.kpi-valor')?.textContent?.trim();
  const deltaKpi = (id: string) => kpi(id).querySelector('.kpi-delta')!;
  const fecha = (nombre: string) =>
    vista().querySelector<HTMLInputElement>(`input[name="${nombre}"]`)!;
  const aplicar = () =>
    [...vista().querySelectorAll('button')].find(
      (b) => b.textContent?.trim() === 'Aplicar filtro',
    )!;
  const filasVendedores = () => [
    ...seccion('Eficiencia del vendedor').querySelectorAll('tbody tr'),
  ];

  async function escribir(nombre: string, valor: string) {
    const input = fecha(nombre);
    input.value = valor;
    input.dispatchEvent(new Event('input'));
    await fixture.whenStable();
  }

  async function responder(respuesta: DashboardEjecutivo = dashboard()) {
    backend.expectOne((r) => r.url === URL_DASHBOARD).flush(respuesta);
    await fixture.whenStable();
  }

  it('al abrir carga sola el último tramo con datos de WideWorldImporters, como el Page_Load', async () => {
    expect(fecha('inicio').value).toBe('2016-01-01');
    expect(fecha('fin').value).toBe('2016-05-31');

    const req = backend.expectOne((r) => r.url === URL_DASHBOARD);
    expect(req.request.params.get('startDate')).toBe('2016-01-01');
    expect(req.request.params.get('endDate')).toBe('2016-05-31');
    req.flush(dashboard());
  });

  it('pinta los cuatro KPIs con el formato de Dashboard.aspx', async () => {
    await responder();

    expect(valorKpi('ingresos')).toBe('$1,000,000.00');
    expect(valorKpi('margen')).toBe('45.50%');
    expect(valorKpi('ordenes')).toBe('1,200');
    expect(valorKpi('ticket')).toBe('$833.33');
  });

  it('compara cada KPI contra el período anterior con flecha, diferencia y porcentaje', async () => {
    await responder();

    expect(deltaKpi('ingresos').textContent?.trim()).toBe(
      '▲ $200,000.00 (25.0%) vs período ant.',
    );
    expect(deltaKpi('ingresos').classList).toContain('sube');
    expect(deltaKpi('margen').textContent?.trim()).toBe('▲ 1.40% (3.2%) vs período ant.');
    expect(deltaKpi('ordenes').textContent?.trim()).toBe('▼ 300 (20.0%) vs período ant.');
    expect(deltaKpi('ordenes').classList).toContain('baja');
    expect(deltaKpi('ticket').textContent?.trim()).toBe('▲ $300.00 (56.3%) vs período ant.');
  });

  it('sin ventas en el período anterior el porcentaje queda en 0 % en lugar de dividir entre cero', async () => {
    await responder(
      dashboard({
        kpis: {
          ...dashboard().kpis,
          previous: { revenue: 0, grossMarginPct: 0, orders: 0, averageTicket: 0 },
        },
      }),
    );

    expect(deltaKpi('ingresos').textContent?.trim()).toBe(
      '▲ $1,000,000.00 (0%) vs período ant.',
    );
  });

  it('muestra ingresos brutos, impuestos y total por mes con el nombre del mes en español', async () => {
    await responder();

    const filas = [...seccion('Ingresos por mes').querySelectorAll('tbody tr')].map((tr) =>
      [...tr.querySelectorAll('td')].map((td) => td.textContent?.trim()),
    );
    expect(filas).toEqual([
      ['Enero 2016', '$4,000,000.00', '$600,000.00', '$4,600,000.00'],
      ['Febrero 2016', '$3,500,000.00', '$525,000.00', '$4,025,000.00'],
    ]);
  });

  it('lista el top 10 de clientes en el orden del backend con su margen neto', async () => {
    await responder();

    const clientes = [...seccion('Top 10 clientes').querySelectorAll('li')].map((li) =>
      li.textContent?.replace(/\s+/g, ' ').trim(),
    );
    expect(clientes).toEqual([
      'Tailspin Toys (Head Office) $120,000.00',
      'Wingtip Toys (Head Office) $110,000.00',
    ]);
  });

  it('reparte el margen por categoría con su porcentaje del total', async () => {
    await responder();

    const categorias = [...seccion('Margen por categoría').querySelectorAll('li')].map((li) =>
      li.textContent?.replace(/\s+/g, ' ').trim(),
    );
    expect(categorias).toEqual([
      'Grupo 2 $300,000.00 (66.7%)',
      'Grupo 7 $150,000.00 (33.3%)',
    ]);
  });

  it('marca en rojo a los vendedores con margen menor a 20 %', async () => {
    await responder();

    const filas = filasVendedores().map((tr) =>
      [...tr.querySelectorAll('td')].map((td) => td.textContent?.trim()),
    );
    expect(filas).toEqual([
      ['1', 'Kayla Woodcock', '$1,250,000.00', '42.10%', '✓ OK'],
      ['2', 'Hudson Onslow', '$500,000.00', '12.50%', '⚠ Margen bajo'],
    ]);
    expect(filasVendedores()[0].classList).not.toContain('alerta');
    expect(filasVendedores()[1].classList).toContain('alerta');
  });

  it('sin datos en el período lo dice en cada visual', async () => {
    await responder(
      dashboard({ monthlyRevenue: [], topCustomers: [], categoryMargins: [], salespeople: [] }),
    );

    for (const nombre of [
      'Ingresos por mes',
      'Top 10 clientes',
      'Margen por categoría',
      'Eficiencia del vendedor',
    ]) {
      expect(seccion(nombre).textContent).toContain('Sin datos para el período seleccionado');
    }
  });

  it('"Aplicar filtro" vuelve a pedir el dashboard con las fechas nuevas', async () => {
    await responder();

    await escribir('inicio', '2015-01-01');
    await escribir('fin', '2015-12-31');
    aplicar().click();
    TestBed.tick();

    const req = backend.expectOne((r) => r.url === URL_DASHBOARD);
    expect(req.request.params.get('startDate')).toBe('2015-01-01');
    expect(req.request.params.get('endDate')).toBe('2015-12-31');
    req.flush(dashboard());
  });

  it('con la fecha inicial después de la final avisa y no pide nada', async () => {
    await responder();

    await escribir('inicio', '2016-06-01');

    expect(aplicar().disabled).toBe(true);
    expect(vista().querySelector('form [role="alert"]')?.textContent).toContain(
      'anterior o igual',
    );
    aplicar().click();
    TestBed.tick();
    expect(backend.match(() => true)).toHaveLength(0);
  });

  it('muestra el panel de error de Dashboard.aspx si el backend falla', async () => {
    backend
      .expectOne((r) => r.url === URL_DASHBOARD)
      .flush(
        { title: 'Server Error', detail: 'Timeout expired' },
        { status: 500, statusText: 'Internal Server Error' },
      );
    await fixture.whenStable();

    const error = vista().querySelector('.panel-error[role="alert"]')?.textContent ?? '';
    expect(error).toContain('Error al cargar datos');
    expect(error).toContain('Server Error');
  });

  it('avisa cuando no hay conexión con el backend', async () => {
    backend
      .expectOne((r) => r.url === URL_DASHBOARD)
      .error(new ProgressEvent('error'), { status: 0 });
    await fixture.whenStable();

    expect(vista().querySelector('.panel-error[role="alert"]')?.textContent).toContain(
      'Sin conexión con el backend.',
    );
  });

  it('tiene un enlace de regreso a System Principal', async () => {
    await responder();

    const volver = [...vista().querySelectorAll('a')].find((a) =>
      a.textContent?.includes('Volver'),
    );
    expect(volver?.getAttribute('href')).toBe('/system-principal');
  });
});
