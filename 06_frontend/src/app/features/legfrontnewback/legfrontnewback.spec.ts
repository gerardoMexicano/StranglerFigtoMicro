import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { APP_CONFIG, AppConfig } from '../../core/config/app-config';
import { Legfrontnewback } from './legfrontnewback';
import { SALES_RESOURCES } from './sales-api.service';

const config: AppConfig = {
  oidc: { authority: 'http://kc/auth/realms/lab', clientId: 'frontend-spa', scope: 'openid' },
  apiBaseUrl: '/api',
  apps: { taxesUrl: '/taxes/Home/Menu', legacyUrl: '/legacy/Menu' },
};

describe('Legfrontnewback', () => {
  let fixture: ComponentFixture<Legfrontnewback>;
  let backend: HttpTestingController;

  beforeEach(async () => {
    TestBed.configureTestingModule({
      imports: [Legfrontnewback],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: APP_CONFIG, useValue: config },
      ],
    });
    backend = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(Legfrontnewback);
    await fixture.whenStable();
  });

  afterEach(() => backend.verify());

  const vista = () => fixture.nativeElement as HTMLElement;
  const fecha = (nombre: string) =>
    vista().querySelector<HTMLInputElement>(`input[name="${nombre}"]`)!;
  const cargar = () =>
    [...vista().querySelectorAll('button')].find(
      (b) => b.textContent?.trim() === 'Cargar registros',
    )!;

  async function escribir(nombre: string, valor: string) {
    const input = fecha(nombre);
    input.value = valor;
    input.dispatchEvent(new Event('input'));
    await fixture.whenStable();
  }

  function peticionesPendientes() {
    TestBed.tick();
    return backend.match((r) => r.url.startsWith('/api/sales/'));
  }

  it('arranca con el rango de la página WebForms y sin pedir nada', () => {
    expect(fecha('inicio').value).toBe('2013-01-01');
    expect(fecha('fin').value).toBe('2016-12-31');
    expect(peticionesPendientes()).toHaveLength(0);
    expect(vista().querySelectorAll('app-sales-table')).toHaveLength(5);
  });

  it('"Cargar registros" pide las cinco tablas a la vez, cada una en su página 1', () => {
    cargar().click();

    const peticiones = peticionesPendientes();

    expect(peticiones.map((p) => p.request.url).sort()).toEqual(
      SALES_RESOURCES.map((r) => `/api/sales/${r}`).sort(),
    );
    for (const p of peticiones) {
      expect(p.request.params.get('startDate')).toBe('2013-01-01');
      expect(p.request.params.get('endDate')).toBe('2016-12-31');
      expect(p.request.params.get('page')).toBe('1');
      p.flush({ items: [], page: 1, pageSize: 50, totalCount: 0, totalPages: 0 });
    }
  });

  it('usa las fechas que escribe el usuario', async () => {
    await escribir('inicio', '2015-01-01');
    await escribir('fin', '2015-06-30');

    cargar().click();
    const peticiones = peticionesPendientes();

    expect(peticiones).toHaveLength(5);
    expect(peticiones[0].request.params.get('startDate')).toBe('2015-01-01');
    expect(peticiones[0].request.params.get('endDate')).toBe('2015-06-30');
    peticiones.forEach((p) =>
      p.flush({ items: [], page: 1, pageSize: 50, totalCount: 0, totalPages: 0 }),
    );
  });

  it('con la fecha inicial después de la final avisa y no pide nada', async () => {
    await escribir('inicio', '2017-01-01');

    expect(cargar().disabled).toBe(true);
    expect(vista().querySelector('[role="alert"]')?.textContent).toContain('anterior o igual');
    cargar().click();
    expect(peticionesPendientes()).toHaveLength(0);
  });

  it('tiene un enlace de regreso al menú', () => {
    const volver = [...vista().querySelectorAll('a')].find((a) =>
      a.textContent?.includes('Volver'),
    );

    expect(volver?.getAttribute('href')).toBe('/menu');
  });
});
