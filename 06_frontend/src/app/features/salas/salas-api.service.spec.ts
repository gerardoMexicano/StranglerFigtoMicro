import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';

import { APP_CONFIG, AppConfig } from '../../core/config/app-config';
import { NuevaReservacion, SalasApiService } from './salas-api.service';

describe('SalasApiService', () => {
  let service: SalasApiService;
  let backend: HttpTestingController;

  beforeEach(() => {
    const config: AppConfig = {
      oidc: { authority: 'http://kc/auth/realms/lab', clientId: 'frontend-spa', scope: 'openid' },
      // Con "/" al final: la URL no debe quedar con "//"
      apiBaseUrl: '/api/',
      apps: { taxesUrl: '/taxes/Home/Menu', legacyUrl: '/legacy/Menu' },
    };
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: APP_CONFIG, useValue: config },
      ],
    });
    service = TestBed.inject(SalasApiService);
    backend = TestBed.inject(HttpTestingController);
  });

  afterEach(() => backend.verify());

  it('salas() pide GET /api/rooms', async () => {
    const respuesta = firstValueFrom(service.salas());

    backend.expectOne({ method: 'GET', url: '/api/rooms' }).flush([]);

    expect(await respuesta).toEqual([]);
  });

  it('reservaciones(fecha) pide la agenda de ese día', async () => {
    const respuesta = firstValueFrom(service.reservaciones('2026-10-31'));

    const peticion = backend.expectOne((r) => r.url === '/api/rooms/bookings');
    expect(peticion.request.method).toBe('GET');
    expect(peticion.request.params.get('date')).toBe('2026-10-31');
    peticion.flush([]);

    expect(await respuesta).toEqual([]);
  });

  it('reservar() manda POST /api/rooms/bookings con las horas sin zona', () => {
    const nueva: NuevaReservacion = {
      roomId: 1,
      startsAt: '2026-10-31T09:00:00',
      endsAt: '2026-10-31T10:00:00',
      subject: 'Junta',
    };

    service.reservar(nueva).subscribe();

    const peticion = backend.expectOne({ method: 'POST', url: '/api/rooms/bookings' });
    expect(peticion.request.body).toEqual(nueva);
    peticion.flush({});
  });

  it('cancelar(id) manda DELETE /api/rooms/bookings/{id}', () => {
    service.cancelar(7).subscribe();

    backend.expectOne({ method: 'DELETE', url: '/api/rooms/bookings/7' }).flush(null, {
      status: 204,
      statusText: 'No Content',
    });
  });
});
