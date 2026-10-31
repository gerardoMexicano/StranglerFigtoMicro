import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import { APP_CONFIG } from '../../core/config/app-config';

/** RoomDto de 05_backend (Application/Features/RoomBookings/Dtos) en camelCase. */
export interface Sala {
  id: number;
  name: string;
  capacity: number;
  location: string;
}

/** BookingDto de 05_backend. Las horas son locales de las salas y sin zona: "2026-10-30T09:00:00". */
export interface Reservacion {
  id: number;
  roomId: number;
  startsAt: string;
  endsAt: string;
  subject: string;
  userName: string;
  isMine: boolean;
  canCancel: boolean;
}

export interface NuevaReservacion {
  roomId: number;
  /** "2026-10-30T09:00:00", sin zona: el backend rechaza "Z" o "-06:00" */
  startsAt: string;
  endsAt: string;
  subject: string;
}

/** /api/rooms de 05_backend (RoomBookingEndpoints.cs). */
@Injectable({ providedIn: 'root' })
export class SalasApiService {
  private readonly http = inject(HttpClient);
  private readonly salasUrl = `${inject(APP_CONFIG).apiBaseUrl.replace(/\/+$/, '')}/rooms`;

  salas(): Observable<Sala[]> {
    return this.http.get<Sala[]>(this.salasUrl);
  }

  /** Las reservaciones de todas las salas en un día ("2026-10-30"). */
  reservaciones(fecha: string): Observable<Reservacion[]> {
    const params = new HttpParams({ fromObject: { date: fecha } });
    return this.http.get<Reservacion[]>(`${this.salasUrl}/bookings`, { params });
  }

  /** Solo usuario y admin; si el horario se empalma el backend responde 409. */
  reservar(reservacion: NuevaReservacion): Observable<Reservacion> {
    return this.http.post<Reservacion>(`${this.salasUrl}/bookings`, reservacion);
  }

  /** La propia, o cualquiera si es admin; si no, 403. */
  cancelar(id: number): Observable<void> {
    return this.http.delete<void>(`${this.salasUrl}/bookings/${id}`);
  }
}
