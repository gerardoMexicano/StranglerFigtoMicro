import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Type } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { AuthService } from '../../../core/auth/auth.service';
import { FakeAuthService } from '../../../core/auth/testing/fake-auth.service';
import { APP_CONFIG, AppConfig } from '../../../core/config/app-config';
import { PayrollSummaryReport } from './nomina-api.service';
import { PaginaAngular } from './pagina-angular';
import { PaginaAntigua } from './pagina-antigua';

const config: AppConfig = {
  oidc: { authority: 'http://kc/auth/realms/lab', clientId: 'frontend-spa', scope: 'openid' },
  apiBaseUrl: '/api',
  apps: { taxesUrl: '/taxes/Home/Menu', legacyUrl: '/legacy/Menu' },
};

const resumenDemo: PayrollSummaryReport = {
  granTotalPercepciones: 25000,
  granTotalDeducciones: 4499.5,
  granTotalNeto: 20500.5,
  totalRecibos: 2,
  costoPorDepartamento: [
    { departamento: 'Tecnología', totalPercepciones: 15000 },
    { departamento: 'Sin Departamento', totalPercepciones: 10000 },
  ],
  nominas: [
    {
      uuid: '11111111-2222-3333-4444-555555555555',
      fecha: '2024-06-15T12:00:00',
      receptorNombre: 'JUAN PEREZ',
      receptorRFC: 'GODE561231GR8',
      total: 12500.5,
      fechaPago: '2024-06-15',
      totalPercepciones: 15000,
      totalDeducciones: 2499.5,
      numEmpleado: 'EMP-001',
      departamento: 'Tecnología',
      puesto: 'Desarrollador Senior',
    },
  ],
};

async function asentar() {
  await new Promise((resolve) => setTimeout(resolve));
  TestBed.tick();
}

describe('Hojas de Second System Broken', () => {
  async function pintarBasico(componente: Type<unknown>): Promise<HTMLElement> {
    const auth = new FakeAuthService();
    auth.session.next(true);
    auth.user.next({ userName: 'juan', name: 'Juan Pérez', roles: ['usuario'] });

    TestBed.configureTestingModule({
      imports: [componente],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: APP_CONFIG, useValue: config },
        { provide: AuthService, useValue: auth },
      ],
    });
    const fixture = TestBed.createComponent(componente);
    await asentar();
    return fixture.nativeElement as HTMLElement;
  }

  it.each([
    [PaginaAntigua, 'Página Antigua'],
    [PaginaAngular, 'Página en Angular'],
  ])(
    'pinta %s con título "%s" y enlace de regreso a /second-system-broken',
    async (comp, titulo) => {
      const vista = await pintarBasico(comp);

      expect(vista.querySelector('h1')?.textContent?.trim()).toBe(titulo);
      expect(vista.querySelector('a')?.getAttribute('href')).toBe('/second-system-broken');
    },
  );
});

describe('PaginaAntigua (B1 — Dashboard Nómina y Carga Masiva XML)', () => {
  let backend: HttpTestingController;
  let auth: FakeAuthService;

  async function montar(roles: string[] = ['usuario']) {
    auth = new FakeAuthService();
    auth.session.next(true);
    auth.user.next({ userName: 'juan', name: 'Juan Pérez', roles });

    TestBed.configureTestingModule({
      imports: [PaginaAntigua],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: APP_CONFIG, useValue: config },
        { provide: AuthService, useValue: auth },
      ],
    });

    backend = TestBed.inject(HttpTestingController);
    const fixture = TestBed.createComponent(PaginaAntigua);
    await asentar();
    return fixture;
  }

  afterEach(() => backend.verify());

  it('consulta /api/taxes/payroll-summary al iniciar y pinta KPIs, departamentos y tabla de recibos', async () => {
    const fixture = await montar(['usuario']);

    backend.expectOne('/api/taxes/payroll-summary').flush(resumenDemo);
    await asentar();

    const vista = fixture.nativeElement as HTMLElement;
    expect(vista.textContent).toContain('Tecnología');
    expect(vista.textContent).toContain('JUAN PEREZ');
    expect(vista.textContent).toContain('EMP-001');
    expect(vista.textContent).toContain('Desarrollador Senior');
  });

  it('permite cargar archivos XML con rol usuario y recarga el resumen', async () => {
    const fixture = await montar(['usuario']);
    backend.expectOne('/api/taxes/payroll-summary').flush(resumenDemo);
    await asentar();

    const archivo = new File(['<cfdi:Comprobante />'], 'nomina1.xml', { type: 'text/xml' });
    (fixture.componentInstance as unknown as { archivosSeleccionados: { set: (f: File[]) => void } })
      .archivosSeleccionados.set([archivo]);
    await asentar();

    const vista = fixture.nativeElement as HTMLElement;
    const btnProcesar = vista.querySelector<HTMLButtonElement>('#btnProcesarXml')!;
    expect(btnProcesar.disabled).toBe(false);

    btnProcesar.click();
    await asentar();

    const post = backend.expectOne('/api/taxes/xml-batch');
    expect(post.request.method).toBe('POST');
    post.flush({
      receivedCount: 1,
      processedCount: 1,
      invalidCount: 0,
      elapsedMs: 18,
      message: 'Se procesaron 1 XMLs válidos de 1 recibidos en 18 ms.',
    });
    await asentar();

    // Recarga automática del resumen
    backend.expectOne('/api/taxes/payroll-summary').flush(resumenDemo);
    await asentar();

    expect(vista.querySelector('[role="alert"]')?.textContent).toContain(
      'Carga con éxito: Se procesaron 1 XMLs válidos',
    );
  });

  it('oculta los controles de carga masiva cuando el rol es invitado', async () => {
    const fixture = await montar(['invitado']);
    backend.expectOne('/api/taxes/payroll-summary').flush(resumenDemo);
    await asentar();

    const vista = fixture.nativeElement as HTMLElement;
    expect(vista.querySelector('#xmlFilesInput')).toBeNull();
    expect(vista.textContent).toContain('Tu rol es de solo consulta');
  });

  it('filtra por rango de fechas y valida que fechaInicio no sea mayor que fechaFin', async () => {
    const fixture = await montar(['admin']);
    backend.expectOne('/api/taxes/payroll-summary').flush(resumenDemo);
    await asentar();

    const comp = fixture.componentInstance as unknown as {
      fechaInicio: { set: (v: string) => void };
      fechaFin: { set: (v: string) => void };
      filtrar: () => void;
    };

    // Rango inválido
    comp.fechaInicio.set('2025-12-31');
    comp.fechaFin.set('2024-01-01');
    comp.filtrar();
    await asentar();

    const vista = fixture.nativeElement as HTMLElement;
    expect(vista.querySelector('[role="alert"]')?.textContent).toContain(
      'La fecha de inicio no puede ser mayor que la fecha final.',
    );

    // Rango válido
    comp.fechaInicio.set('2024-06-01');
    comp.fechaFin.set('2024-06-30');
    comp.filtrar();
    await asentar();

    const req = backend.expectOne(
      (r) =>
        r.url === '/api/taxes/payroll-summary' &&
        r.params.get('startDate') === '2024-06-01' &&
        r.params.get('endDate') === '2024-06-30',
    );
    req.flush(resumenDemo);
  });
});
