import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import { APP_CONFIG } from '../../core/config/app-config';
import { Ubicacion } from './geolocalizacion';

/** CheckInDto de 05_backend (Application/Features/Attendance/Dtos) en camelCase. */
export interface CheckIn {
  id: number;
  latitude: number;
  longitude: number;
  accuracyMeters: number;
  /** ISO 8601 con zona: la hora la pone SQL Server */
  registeredAt: string;
}

/** Cuántos check-ins recientes muestra la vista. */
export const HISTORIAL_TAMANO = 10;

/** /api/attendance/check-ins de 05_backend (AttendanceEndpoints.cs). */
@Injectable({ providedIn: 'root' })
export class AsistenciaApiService {
  private readonly http = inject(HttpClient);
  private readonly checkInsUrl = `${inject(APP_CONFIG).apiBaseUrl.replace(/\/+$/, '')}/attendance/check-ins`;

  /** Solo usuario y admin; con invitado el backend responde 403. */
  registrar(ubicacion: Ubicacion): Observable<CheckIn> {
    return this.http.post<CheckIn>(this.checkInsUrl, {
      latitude: ubicacion.latitud,
      longitude: ubicacion.longitud,
      accuracyMeters: ubicacion.precisionMetros,
    });
  }

  /** Los últimos check-ins de quien tiene la sesión, del más reciente al más antiguo. */
  misRegistros(take = HISTORIAL_TAMANO): Observable<CheckIn[]> {
    const params = new HttpParams({ fromObject: { take } });
    return this.http.get<CheckIn[]>(`${this.checkInsUrl}/mine`, { params });
  }
}
