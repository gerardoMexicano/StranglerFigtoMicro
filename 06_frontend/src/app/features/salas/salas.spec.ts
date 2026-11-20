import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { AuthService } from '../../core/auth/auth.service';
import { FakeAuthService } from '../../core/auth/testing/fake-auth.service';
import { APP_CONFIG, AppConfig } from '../../core/config/app-config';
import { Reservacion, Sala } from './salas-api.service';
import { Salas } from './salas';

const config: AppConfig = {
  oidc: { authority: 'http://kc/auth/realms/lab', clientId: 'frontend-spa', scope: 'openid' },
  apiBaseUrl: '/api',
  apps: { taxesUrl: '/taxes/Home/Menu', legacyUrl: '/legacy/Menu' },
};

const SALAS: Sala[] = [
  { id: 1, name: 'Sala Juárez', capacity: 8, location: 'Planta baja' },
  { id: 2, name: 'Sala Hidalgo', capacity: 12, location: 'Primer piso' },
];

// "Hoy" en todas las pruebas: viernes 30/10/2026 a las 10:10, hora local
const HOY = '2026-10-30';

const DE_ANA: Reservacion = {
  id: 11,
  roomId: 2,
  startsAt: `${HOY}T11:00:00`,
  endsAt: `${HOY}T12:00:00`,
  subject: 'Capacitación',
  userName: 'ana',
  isMine: false,
  canCancel: false,
};

const MIA: Reservacion = {
  ...DE_ANA,
  id: 12,
  roomId: 1,
  startsAt: `${HOY}T15:00:00`,
  endsAt: `${HOY}T15:30:00`,
  subject: 'Revisión',
  userName: 'juan',
  isMine: true,
  canCancel: true,
};

describe('Salas', () => {
  let fixture: ComponentFixture<Salas>;
  let backend: HttpTestingController;

  beforeEach(() => {
    vi.useFakeTimers({ toFake: ['Date'] });
    vi.setSystemTime(new Date(2026, 9, 30, 10, 10));
  });

  afterEach(() => {
    backend.verify();
    vi.useRealTimers();
    vi.restoreAllMocks();
  });

  async function pintar(roles: string[], reservaciones: Reservacion[] = [DE_ANA, MIA]) {
    const auth = new FakeAuthService();
    auth.session.next(true);
    auth.user.next({ userName: 'juan', name: 'Juan Pérez', roles });

    TestBed.configureTestingModule({
      imports: [Salas],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: APP_CONFIG, useValue: config },
        { provide: AuthService, useValue: auth },
      ],
    });
    backend = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(Salas);
    await asentar();

    backend.expectOne('/api/rooms').flush(SALAS);
    agenda(HOY, reservaciones);
    await asentar();
  }

  /** whenStable() esperaría a las peticiones pendientes; aquí solo corren promesas y la vista. */
  async function asentar() {
    await new Promise((resolve) => setTimeout(resolve));
    TestBed.tick();
  }

  function agenda(fecha: string, reservaciones: Reservacion[]) {
    const peticion = backend.expectOne((r) => r.url === '/api/rooms/bookings');
    expect(peticion.request.params.get('date')).toBe(fecha);
    peticion.flush(reservaciones);
  }

  const vista = () => fixture.nativeElement as HTMLElement;
  const aviso = () => vista().querySelector('.aviso')?.textContent?.trim();
  const reservadas = () =>
    [...vista().querySelectorAll<HTMLTableCellElement>('td.reservada')].map((td) => ({
      texto: td.textContent?.replace(/\s+/g, ' ').trim(),
      filas: td.rowSpan,
      mia: td.classList.contains('mia'),
      cancelable: td.querySelector('button') !== null,
    }));

  function seleccionar(nombre: string, valor: string) {
    const select = vista().querySelector<HTMLSelectElement>(`select[name="${nombre}"]`)!;
    select.selectedIndex = [...select.options].findIndex((o) => o.textContent?.trim() === valor);
    select.dispatchEvent(new Event('change'));
  }

  function escribirAsunto(texto: string) {
    const input = vista().querySelector<HTMLInputElement>('#asunto')!;
    input.value = texto;
    input.dispatchEvent(new Event('input'));
  }

  async function enviar() {
    vista().querySelector('form')!.dispatchEvent(new Event('submit'));
    await asentar();
  }

  it('pinta la agenda de hoy: una columna por sala y cada reservación en su horario', async () => {
    await pintar(['usuario']);

    const encabezados = [...vista().querySelectorAll('thead th')].map((th) =>
      th.textContent?.replace(/\s+/g, ' ').trim(),
    );
    expect(encabezados).toEqual([
      'Hora',
      'Sala Juárez 8 personas · Planta baja',
      'Sala Hidalgo 12 personas · Primer piso',
    ]);
    expect(vista().querySelectorAll('tbody tr')).toHaveLength(24);
    expect(reservadas()).toEqual([
      { texto: 'Capacitación 11:00–12:00 · ana', filas: 2, mia: false, cancelable: false },
      { texto: 'Revisión 15:00–15:30 · tú Cancelar', filas: 1, mia: true, cancelable: true },
    ]);
  });

  it('usuario: el formulario propone el primer bloque que no ha empezado', async () => {
    await pintar(['usuario']);

    const valor = (nombre: string) =>
      vista()
        .querySelector<HTMLSelectElement>(`select[name="${nombre}"]`)!
        .selectedOptions[0]?.textContent?.trim();
    expect(valor('sala')).toBe('Sala Juárez');
    expect(valor('inicio')).toBe('10:30');
    expect(valor('fin')).toBe('11:00');
    // Los bloques de hoy que ya pasaron no se ofrecen en la agenda
    expect(vista().querySelector('[aria-label="Reservar Sala Juárez a las 10:00"]')).toBeNull();
    expect(vista().querySelector('[aria-label="Reservar Sala Juárez a las 10:30"]')).not.toBeNull();
  });

  it('usuario: reserva con las horas locales sin zona, avisa y recarga la agenda', async () => {
    await pintar(['usuario']);

    seleccionar('sala', 'Sala Hidalgo');
    seleccionar('inicio', '13:00');
    await asentar();
    seleccionar('fin', '14:30');
    escribirAsunto('  Junta de planeación ');
    await asentar();
    await enviar();

    const post = backend.expectOne({ method: 'POST', url: '/api/rooms/bookings' });
    expect(post.request.body).toEqual({
      roomId: 2,
      startsAt: `${HOY}T13:00:00`,
      endsAt: `${HOY}T14:30:00`,
      subject: 'Junta de planeación',
    });
    post.flush({ ...MIA, id: 20 }, { status: 201, statusText: 'Created' });
    await asentar();
    agenda(HOY, [DE_ANA, MIA]);
    await asentar();

    expect(aviso()).toBe('Reservaste Sala Hidalgo de 13:00 a 14:30.');
    expect(vista().querySelector<HTMLInputElement>('#asunto')!.value).toBe('');
  });

  it('clic en un bloque libre lo pone en el formulario', async () => {
    await pintar(['admin']);

    vista()
      .querySelector<HTMLButtonElement>('[aria-label="Reservar Sala Hidalgo a las 16:00"]')!
      .click();
    await asentar();

    const valor = (nombre: string) =>
      vista()
        .querySelector<HTMLSelectElement>(`select[name="${nombre}"]`)!
        .selectedOptions[0]?.textContent?.trim();
    expect(valor('sala')).toBe('Sala Hidalgo');
    expect(valor('inicio')).toBe('16:00');
    expect(valor('fin')).toBe('16:30');
  });

  it('si el horario se empalma muestra el detalle del 409', async () => {
    await pintar(['usuario']);
    escribirAsunto('Junta');
    await asentar();
    await enviar();

    backend
      .expectOne('/api/rooms/bookings')
      .flush(
        { detail: 'La sala ya está reservada en parte de ese horario (10:30–11:00).' },
        { status: 409, statusText: 'Conflict' },
      );
    await asentar();
    agenda(HOY, [DE_ANA, MIA]);
    await asentar();

    expect(vista().querySelector('[role="alert"]')?.textContent).toContain(
      'La sala ya está reservada en parte de ese horario',
    );
  });

  it('sin asunto no deja reservar', async () => {
    await pintar(['usuario']);

    expect(vista().querySelector<HTMLButtonElement>('form button[type="submit"]')!.disabled).toBe(
      true,
    );
    await enviar();
    backend.expectNone({ method: 'POST', url: '/api/rooms/bookings' });
  });

  it('cancela la propia después de confirmar y recarga la agenda', async () => {
    const confirmar = vi.spyOn(window, 'confirm').mockReturnValue(true);
    await pintar(['usuario']);

    vista().querySelector<HTMLButtonElement>('td.mia button')!.click();
    await asentar();

    expect(confirmar).toHaveBeenCalledWith('¿Cancelar "Revisión" (15:00 a 15:30)?');
    backend
      .expectOne({ method: 'DELETE', url: '/api/rooms/bookings/12' })
      .flush(null, { status: 204, statusText: 'No Content' });
    await asentar();
    agenda(HOY, [DE_ANA]);
    await asentar();

    expect(aviso()).toBe('Cancelaste "Revisión" (15:00 a 15:30).');
    expect(reservadas()).toHaveLength(1);
  });

  it('si no confirma no cancela', async () => {
    vi.spyOn(window, 'confirm').mockReturnValue(false);
    await pintar(['usuario']);

    vista().querySelector<HTMLButtonElement>('td.mia button')!.click();
    await asentar();

    backend.expectNone({ method: 'DELETE' });
  });

  it('invitado: ve la agenda pero no hay formulario, bloques para elegir ni cancelar', async () => {
    await pintar(['invitado'], [{ ...MIA, canCancel: false }]);

    expect(vista().textContent).toContain('puedes consultar la agenda, pero no reservar');
    expect(vista().querySelector('form')).toBeNull();
    expect(vista().querySelector('.boton-elegir')).toBeNull();
    expect(vista().querySelector('.boton-cancelar')).toBeNull();
    expect(reservadas()).toHaveLength(1);
  });

  it('cambiar de día pide la agenda de ese día', async () => {
    await pintar(['usuario']);

    vista().querySelector<HTMLButtonElement>('[aria-label="Día siguiente"]')!.click();
    await asentar();
    agenda('2026-10-31', []);
    await asentar();

    expect(vista().querySelector<HTMLInputElement>('input[type="date"]')!.value).toBe('2026-10-31');
    expect(reservadas()).toEqual([]);
    // Mañana todos los bloques se pueden elegir, desde las 08:00
    expect(vista().querySelector('[aria-label="Reservar Sala Juárez a las 08:00"]')).not.toBeNull();
  });

  it('si no carga la agenda lo dice', async () => {
    await pintar(['usuario'], []);

    vista().querySelector<HTMLButtonElement>('[aria-label="Día anterior"]')!.click();
    await asentar();
    backend
      .expectOne((r) => r.url === '/api/rooms/bookings')
      .flush(null, { status: 500, statusText: 'Server Error' });
    await asentar();

    expect(vista().querySelector('[role="alert"]')?.textContent).toContain(
      'No se pudo cargar la agenda',
    );
  });
});
