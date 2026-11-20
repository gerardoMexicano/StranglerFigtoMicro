import { HttpHeaders, provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Type } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { AuthService } from '../../../core/auth/auth.service';
import { FakeAuthService } from '../../../core/auth/testing/fake-auth.service';
import { APP_CONFIG, AppConfig } from '../../../core/config/app-config';
import { Configuracion, PREFERENCIAS_STORAGE_KEY } from './configuracion';
import { Dashboard } from './dashboard';
import { Reportes } from './reportes';

const config: AppConfig = {
  oidc: { authority: 'http://kc/auth/realms/lab', clientId: 'frontend-spa', scope: 'openid' },
  apiBaseUrl: '/api',
  apps: { taxesUrl: '/taxes/Home/Menu', legacyUrl: '/legacy/Menu' },
};

async function asentar() {
  await new Promise((resolve) => setTimeout(resolve));
  TestBed.tick();
}

describe('Hojas de System Principal', () => {
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
    [Reportes, 'Reportes'],
    [Configuracion, 'Configuración'],
  ])('pinta %s con título "%s" y enlace de regreso a /system-principal', async (comp, titulo) => {
    const vista = await pintarBasico(comp);

    expect(vista.querySelector('h1')?.textContent?.trim()).toBe(titulo);
    expect(vista.querySelector('a')?.getAttribute('href')).toBe('/system-principal');
  });
});

describe('Reportes (L2 — Generación Masiva de PDFs CFDI 4.0)', () => {
  let backend: HttpTestingController;

  async function montar() {
    TestBed.configureTestingModule({
      imports: [Reportes],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: APP_CONFIG, useValue: config },
      ],
    });
    backend = TestBed.inject(HttpTestingController);
    const fixture = TestBed.createComponent(Reportes);
    await asentar();
    return fixture;
  }

  afterEach(() => backend.verify());

  it('muestra error si se intenta generar sin seleccionar archivos XML', async () => {
    const fixture = await montar();
    const vista = fixture.nativeElement as HTMLElement;

    vista.querySelector<HTMLButtonElement>('#btnGenerarPDFs')!.click();
    await asentar();

    expect(vista.querySelector('[role="alert"]')?.textContent).toContain(
      'Por favor, selecciona al menos un archivo XML.',
    );
  });

  it('envía los archivos a /api/reports/cfdi-pdf-batch y muestra el conteo y enlace de descarga ZIP', async () => {
    const fixture = await montar();
    const archivo = new File(['<cfdi:Comprobante />'], 'factura1.xml', { type: 'text/xml' });
    (fixture.componentInstance as unknown as { archivos: { set: (f: File[]) => void } }).archivos.set([
      archivo,
    ]);
    await asentar();

    const vista = fixture.nativeElement as HTMLElement;
    vista.querySelector<HTMLButtonElement>('#btnGenerarPDFs')!.click();
    await asentar();

    const req = backend.expectOne('/api/reports/cfdi-pdf-batch');
    expect(req.request.method).toBe('POST');

    const zipBlob = new Blob(['PK-fake-zip'], { type: 'application/zip' });
    req.flush(zipBlob, {
      headers: new HttpHeaders({
        'X-Received-Count': '2',
        'X-Valid-Count': '1',
        'X-Invalid-Count': '1',
      }),
    });
    await asentar();

    expect(vista.querySelector('[role="alert"]')?.textContent).toContain(
      'Se generaron 1 PDFs de 2 XMLs.',
    );
    const link = vista.querySelector<HTMLAnchorElement>('#linkDescargarZip');
    expect(link).not.toBeNull();
    expect(link?.getAttribute('download')).toBe('ReportesGenerados.zip');
  });
});

describe('Configuracion (L3 — Perfil OIDC, Entorno y Preferencias Locales)', () => {
  let backend: HttpTestingController;

  async function montar(roles: string[] = ['admin']) {
    localStorage.removeItem(PREFERENCIAS_STORAGE_KEY);
    const auth = new FakeAuthService();
    auth.session.next(true);
    auth.user.next({ userName: 'admin', name: 'Administrador', roles });

    TestBed.configureTestingModule({
      imports: [Configuracion],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: APP_CONFIG, useValue: config },
        { provide: AuthService, useValue: auth },
      ],
    });
    backend = TestBed.inject(HttpTestingController);
    const fixture = TestBed.createComponent(Configuracion);
    await asentar();
    return fixture;
  }

  afterEach(() => {
    backend.verify();
    localStorage.removeItem(PREFERENCIAS_STORAGE_KEY);
  });

  it('consulta /api/system/me al iniciar y muestra la identidad validada, roles y URLs del entorno', async () => {
    const fixture = await montar(['admin']);

    backend.expectOne('/api/system/me').flush({
      userId: 'kc-admin-001',
      userName: 'admin',
      email: 'admin@laboratorio.local',
      roles: ['admin'],
    });
    await asentar();

    const vista = fixture.nativeElement as HTMLElement;
    expect(vista.textContent).toContain('kc-admin-001');
    expect(vista.textContent).toContain('admin@laboratorio.local');
    expect(vista.textContent).toContain('http://kc/auth/realms/lab');
    expect(vista.textContent).toContain('Token JWT validado en /api/system/me');
  });

  it('guarda y restaura preferencias visuales en localStorage', async () => {
    const fixture = await montar(['usuario']);
    backend.expectOne('/api/system/me').flush({
      userId: 'kc-user-01',
      userName: 'juan',
      email: null,
      roles: ['usuario'],
    });
    await asentar();

    const comp = fixture.componentInstance as unknown as {
      tema: { set: (v: string) => void };
      densidad: { set: (v: string) => void };
      tamanoPagina: { set: (v: number) => void };
      guardarPreferencias: () => void;
      restaurarPreferencias: () => void;
    };

    comp.tema.set('oscuro');
    comp.densidad.set('compacta');
    comp.tamanoPagina.set(100);
    comp.guardarPreferencias();
    await asentar();

    const guardado = JSON.parse(localStorage.getItem(PREFERENCIAS_STORAGE_KEY) ?? '{}');
    expect(guardado).toEqual({ tema: 'oscuro', densidad: 'compacta', tamanoPagina: 100 });

    comp.restaurarPreferencias();
    await asentar();
    expect(localStorage.getItem(PREFERENCIAS_STORAGE_KEY)).toBeNull();
  });
});
