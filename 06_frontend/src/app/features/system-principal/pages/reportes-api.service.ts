import { HttpClient, HttpResponse } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { map, Observable } from 'rxjs';

import { APP_CONFIG } from '../../../core/config/app-config';

export interface CfdiPdfBatchDownload {
  blob: Blob;
  fileName: string;
  receivedCount: number;
  validCount: number;
  invalidCount: number;
}

@Injectable({ providedIn: 'root' })
export class ReportesApiService {
  private readonly http = inject(HttpClient);
  private readonly reportsUrl = `${inject(APP_CONFIG).apiBaseUrl.replace(/\/+$/, '')}/reports`;

  generarPdfs(files: readonly File[]): Observable<CfdiPdfBatchDownload> {
    const formData = new FormData();
    for (const file of files) {
      formData.append('xmlFiles', file, file.name);
    }

    return this.http
      .post(`${this.reportsUrl}/cfdi-pdf-batch`, formData, {
        observe: 'response',
        responseType: 'blob',
      })
      .pipe(map((res: HttpResponse<Blob>) => extraerResultadoZip(res, files.length)));
  }
}

function extraerResultadoZip(
  response: HttpResponse<Blob>,
  defaultCount: number,
): CfdiPdfBatchDownload {
  const receivedHeader = response.headers.get('X-Received-Count');
  const validHeader = response.headers.get('X-Valid-Count');
  const invalidHeader = response.headers.get('X-Invalid-Count');

  const receivedCount = receivedHeader ? Number.parseInt(receivedHeader, 10) : defaultCount;
  const validCount = validHeader ? Number.parseInt(validHeader, 10) : defaultCount;
  const invalidCount = invalidHeader ? Number.parseInt(invalidHeader, 10) : 0;

  return {
    blob: response.body ?? new Blob([], { type: 'application/zip' }),
    fileName: 'ReportesGenerados.zip',
    receivedCount: Number.isNaN(receivedCount) ? defaultCount : receivedCount,
    validCount: Number.isNaN(validCount) ? defaultCount : validCount,
    invalidCount: Number.isNaN(invalidCount) ? 0 : invalidCount,
  };
}
