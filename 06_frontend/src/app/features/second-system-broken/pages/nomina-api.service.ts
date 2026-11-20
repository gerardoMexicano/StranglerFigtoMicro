import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import { APP_CONFIG } from '../../../core/config/app-config';

export interface PayrollReceiptItem {
  uuid: string;
  fecha: string;
  receptorNombre: string | null;
  receptorRFC: string;
  total: number;
  fechaPago: string | null;
  totalPercepciones: number;
  totalDeducciones: number;
  numEmpleado: string | null;
  departamento: string | null;
  puesto: string | null;
}

export interface DepartmentPayrollCost {
  departamento: string;
  totalPercepciones: number;
}

export interface PayrollSummaryReport {
  granTotalPercepciones: number;
  granTotalDeducciones: number;
  granTotalNeto: number;
  totalRecibos: number;
  costoPorDepartamento: DepartmentPayrollCost[];
  nominas: PayrollReceiptItem[];
}

export interface BulkXmlUploadResult {
  receivedCount: number;
  processedCount: number;
  invalidCount: number;
  elapsedMs: number;
  message: string;
}

@Injectable({ providedIn: 'root' })
export class NominaApiService {
  private readonly http = inject(HttpClient);
  private readonly taxesUrl = `${inject(APP_CONFIG).apiBaseUrl.replace(/\/+$/, '')}/taxes`;

  obtenerResumen(startDate?: string, endDate?: string): Observable<PayrollSummaryReport> {
    let params = new HttpParams();
    if (startDate) {
      params = params.set('startDate', startDate);
    }
    if (endDate) {
      params = params.set('endDate', endDate);
    }
    return this.http.get<PayrollSummaryReport>(`${this.taxesUrl}/payroll-summary`, { params });
  }

  cargarXmls(files: readonly File[]): Observable<BulkXmlUploadResult> {
    const formData = new FormData();
    for (const file of files) {
      formData.append('xmlFiles', file, file.name);
    }
    return this.http.post<BulkXmlUploadResult>(`${this.taxesUrl}/xml-batch`, formData);
  }
}
