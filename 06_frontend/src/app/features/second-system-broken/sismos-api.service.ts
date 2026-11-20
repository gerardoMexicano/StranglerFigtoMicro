import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import { APP_CONFIG } from '../../core/config/app-config';

/** Parámetros de la comparativa; mismos valores por defecto que PaginaAngular en MVC. */
export interface ConsultaSismos {
  count: number;
  minMagnitude: number;
  seed: number;
}

export const CONSULTA_SISMOS_POR_DEFECTO: ConsultaSismos = {
  count: 5000,
  minMagnitude: 1.0,
  seed: 2026,
};

/** SeismicPointDto de 05_backend en camelCase. */
export interface PuntoSismico {
  lat: number;
  lng: number;
  mag: number;
}

/** SeismicComparisonResultDto de 05_backend (Application/Features/Taxes/Dtos) en camelCase. */
export interface ResultadoSismos {
  engine: string;
  requestedCount: number;
  returnedCount: number;
  minMagnitude: number;
  seed: number;
  averageMagnitude: number;
  maxMagnitude: number;
  serverElapsedMs: number;
  memoryMegabytes: number;
  points: PuntoSismico[];
}

/** GET /api/taxes/seismic-geojson de 05_backend (TaxesEndpoints.cs). */
@Injectable({ providedIn: 'root' })
export class SismosApiService {
  private readonly http = inject(HttpClient);
  private readonly url = `${inject(APP_CONFIG).apiBaseUrl.replace(/\/+$/, '')}/taxes/seismic-geojson`;

  consultar(consulta: ConsultaSismos): Observable<ResultadoSismos> {
    const params = new HttpParams({
      fromObject: {
        count: consulta.count,
        minMagnitude: consulta.minMagnitude,
        seed: consulta.seed,
      },
    });
    return this.http.get<ResultadoSismos>(this.url, { params });
  }
}
