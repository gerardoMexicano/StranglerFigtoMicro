import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import { APP_CONFIG } from '../../core/config/app-config';

export interface RangoDashboard {
  /** yyyy-MM-dd */
  startDate: string;
  /** yyyy-MM-dd */
  endDate: string;
}

/** WideWorldImporters llega hasta 2016-05-31: el año en curso de Dashboard.aspx saldría vacío. */
export const RANGO_DASHBOARD_POR_DEFECTO: RangoDashboard = {
  startDate: '2016-01-01',
  endDate: '2016-05-31',
};

/** DTOs de 05_backend (Application/Features/Dashboard/Dtos) en camelCase. */
export interface KpisDashboard {
  revenue: number;
  grossMarginPct: number;
  orders: number;
  averageTicket: number;
}

export interface KpisEjecutivos {
  current: KpisDashboard;
  previous: KpisDashboard;
  previousStartDate: string;
  previousEndDate: string;
}

export interface IngresoMensual {
  year: number;
  month: number;
  grossRevenue: number;
  taxes: number;
  totalRevenue: number;
}

export interface ClienteRentable {
  customer: string;
  netMargin: number;
}

export interface MargenCategoria {
  category: string;
  grossMargin: number;
}

export interface EficienciaVendedor {
  salesperson: string;
  revenue: number;
  marginPct: number;
  lowMargin: boolean;
}

export interface DashboardEjecutivo {
  startDate: string;
  endDate: string;
  kpis: KpisEjecutivos;
  monthlyRevenue: IngresoMensual[];
  topCustomers: ClienteRentable[];
  categoryMargins: MargenCategoria[];
  salespeople: EficienciaVendedor[];
}

/** GET /api/dashboard/executive de 05_backend (DashboardEndpoints.cs). */
@Injectable({ providedIn: 'root' })
export class DashboardApiService {
  private readonly http = inject(HttpClient);
  private readonly url = `${inject(APP_CONFIG).apiBaseUrl.replace(/\/+$/, '')}/dashboard/executive`;

  consultar(rango: RangoDashboard): Observable<DashboardEjecutivo> {
    const params = new HttpParams({
      fromObject: { startDate: rango.startDate, endDate: rango.endDate },
    });
    return this.http.get<DashboardEjecutivo>(this.url, { params });
  }
}
