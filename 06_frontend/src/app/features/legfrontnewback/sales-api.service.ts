import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import { APP_CONFIG } from '../../core/config/app-config';

/** Endpoints de /api/sales en 05_backend (SalesEndpoints.cs), uno por tabla de LEgFrontNEWBACK. */
export type SalesResource =
  'customer-transactions' | 'invoice-lines' | 'invoices' | 'order-lines' | 'orders';

export const SALES_RESOURCES: readonly SalesResource[] = [
  'customer-transactions',
  'invoice-lines',
  'invoices',
  'order-lines',
  'orders',
];

/** Igual que el GridView de WebForms: 50 filas por página, paginado en el servidor. */
export const SALES_PAGE_SIZE = 50;

export interface SalesQuery {
  /** yyyy-MM-dd */
  startDate: string;
  /** yyyy-MM-dd */
  endDate: string;
  page: number;
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

/** Las tablas se pintan con las columnas que traiga cada fila, como AutoGenerateColumns. */
export type SalesRow = Record<string, unknown>;

@Injectable({ providedIn: 'root' })
export class SalesApiService {
  private readonly http = inject(HttpClient);
  private readonly salesUrl = `${inject(APP_CONFIG).apiBaseUrl.replace(/\/+$/, '')}/sales`;

  getPage(resource: SalesResource, query: SalesQuery): Observable<PagedResult<SalesRow>> {
    const params = new HttpParams({
      fromObject: {
        startDate: query.startDate,
        endDate: query.endDate,
        page: query.page,
        pageSize: SALES_PAGE_SIZE,
      },
    });
    return this.http.get<PagedResult<SalesRow>>(`${this.salesUrl}/${resource}`, { params });
  }
}
