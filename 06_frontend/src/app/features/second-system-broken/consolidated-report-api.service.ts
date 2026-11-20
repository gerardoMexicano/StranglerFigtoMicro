import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import { APP_CONFIG } from '../../core/config/app-config';

/** Igual que PaginaAntiguaNuevoBackend en MVC: 50 registros por página, paginado en el servidor. */
export const CONSOLIDATED_REPORT_PAGE_SIZE = 50;

export interface ConsolidatedReportQuery {
  /** yyyy-MM-dd */
  startDate: string;
  /** yyyy-MM-dd */
  endDate: string;
  page: number;
}

/** ConsolidatedReportItemDto de 05_backend (Application/Features/Taxes/Dtos) en camelCase. */
export interface ConsolidatedReportItem {
  uuid: string;
  emisorRFC: string;
  receptorRFC: string;
  total: number | null;
  fecha: string;
  tipoComplemento: string | null;
  numEmpleado: string | null;
  departamento: string | null;
  puesto: string | null;
  totalPercepciones: number | null;
  totalDeducciones: number | null;
  tipoMovimiento: string | null;
  concepto: string | null;
  importe: number | null;
}

/** PagedResult<T> de 05_backend (Application/Common/Models/PagedResult.cs) en camelCase. */
export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasPreviousPage: boolean;
  hasNextPage: boolean;
}

/** GET /api/taxes/consolidated-report de 05_backend (TaxesEndpoints.cs). */
@Injectable({ providedIn: 'root' })
export class ConsolidatedReportApiService {
  private readonly http = inject(HttpClient);
  private readonly url = `${inject(APP_CONFIG).apiBaseUrl.replace(/\/+$/, '')}/taxes/consolidated-report`;

  getPage(query: ConsolidatedReportQuery): Observable<PagedResult<ConsolidatedReportItem>> {
    const params = new HttpParams({
      fromObject: {
        startDate: query.startDate,
        endDate: query.endDate,
        page: query.page,
        pageSize: CONSOLIDATED_REPORT_PAGE_SIZE,
      },
    });
    return this.http.get<PagedResult<ConsolidatedReportItem>>(this.url, { params });
  }
}
