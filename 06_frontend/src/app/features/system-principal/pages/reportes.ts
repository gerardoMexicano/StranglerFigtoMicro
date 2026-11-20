import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, OnDestroy, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';

import { ReportesApiService } from './reportes-api.service';

export interface AvisoReporte {
  tipo: 'exito' | 'error';
  texto: string;
}

/**
 * L2 — Reportes: migración a Angular de /legacy/Reportes.aspx.
 * Recibe archivos XML CFDI 4.0, los envía a POST /api/reports/cfdi-pdf-batch en 05_backend
 * y ofrece la descarga inmediata del archivo ReportesGenerados.zip con los PDFs generados.
 */
@Component({
  selector: 'app-reportes',
  imports: [RouterLink],
  template: `
    <main class="pagina">
      <a class="boton boton-texto" routerLink="/system-principal">← Volver a System Principal</a>

      <section class="tarjeta-reportes">
        <div class="icono" aria-hidden="true">📋</div>
        <h1>Reportes</h1>
        <p class="descripcion">
          Módulo para la lectura de comprobantes CFDI 4.0 (.xml) y generación masiva de reportes PDF
          empaquetados en ZIP desde 05_backend (.NET 10).
        </p>

        <div class="zona-archivos">
          <label for="fuXmlFiles" class="etiqueta-archivo">Sube tus archivos XML CFDI 4.0:</label>
          <input
            id="fuXmlFiles"
            type="file"
            multiple
            accept=".xml"
            (change)="seleccionarArchivos($event)"
            [disabled]="procesando()"
          />
          @if (archivos().length > 0) {
            <p class="conteo-seleccionados">
              {{ archivos().length }} archivo(s) XML seleccionado(s).
            </p>
          }
        </div>

        <div class="acciones">
          <button
            id="btnGenerarPDFs"
            type="button"
            class="boton-accion"
            (click)="generarPdfs()"
            [disabled]="procesando()"
          >
            {{ procesando() ? 'Generando PDFs...' : 'Generar PDFs (Proceso Pesado)' }}
          </button>
        </div>

        @if (aviso(); as msg) {
          <div
            class="estado-mensaje"
            [class.estado-exito]="msg.tipo === 'exito'"
            [class.estado-error]="msg.tipo === 'error'"
            role="alert"
          >
            <span>{{ msg.texto }}</span>
            @if (zipDownloadUrl(); as url) {
              <a
                id="linkDescargarZip"
                class="enlace-descarga"
                [href]="url"
                download="ReportesGenerados.zip"
              >
                Descargar ZIP (ReportesGenerados.zip)
              </a>
            }
          </div>
        }
      </section>
    </main>
  `,
  styles: `
    .pagina {
      max-width: 720px;
      margin: 0 auto;
      padding: 1.5rem 1rem 3rem;
    }
    .tarjeta-reportes {
      background: #ffffff;
      border: 1px solid #e2e8f0;
      border-radius: 12px;
      box-shadow: 0 4px 20px rgba(15, 23, 42, 0.08);
      padding: 2rem;
      margin-top: 1rem;
      text-align: center;
    }
    .icono {
      font-size: 3.5rem;
      margin-bottom: 0.75rem;
    }
    h1 {
      color: #1a1a2e;
      font-size: 1.9rem;
      margin: 0 0 0.5rem;
    }
    .descripcion {
      color: #475569;
      font-size: 1.05rem;
      line-height: 1.6;
      margin-bottom: 1.5rem;
    }
    .zona-archivos {
      margin-bottom: 1.25rem;
      text-align: left;
      background: #f8fafc;
      padding: 1rem;
      border-radius: 8px;
      border: 1px dashed #cbd5e1;
    }
    .etiqueta-archivo {
      display: block;
      margin-bottom: 0.5rem;
      font-weight: 600;
      color: #1e293b;
    }
    .conteo-seleccionados {
      margin: 0.5rem 0 0;
      font-size: 0.88rem;
      color: #0f766e;
      font-weight: 600;
    }
    .boton-accion {
      display: inline-block;
      padding: 0.75rem 1.5rem;
      background: #e94560;
      color: #ffffff;
      border-radius: 8px;
      font-weight: 700;
      border: none;
      cursor: pointer;
      font-size: 1rem;
    }
    .boton-accion:disabled {
      opacity: 0.65;
      cursor: not-allowed;
    }
    .estado-mensaje {
      margin-top: 1.25rem;
      padding: 1rem;
      border-radius: 8px;
      font-weight: 600;
      display: flex;
      flex-direction: column;
      gap: 0.6rem;
      align-items: center;
    }
    .estado-exito {
      background: #ecfdf5;
      color: #065f46;
      border: 1px solid #a7f3d0;
    }
    .estado-error {
      background: #fef2f2;
      color: #991b1b;
      border: 1px solid #fecaca;
    }
    .enlace-descarga {
      display: inline-block;
      padding: 0.5rem 1rem;
      background: #0f3460;
      color: #ffffff;
      border-radius: 6px;
      text-decoration: none;
      font-weight: 700;
    }
  `,
})
export class Reportes implements OnDestroy {
  private readonly api = inject(ReportesApiService);

  protected readonly archivos = signal<readonly File[]>([]);
  protected readonly procesando = signal(false);
  protected readonly aviso = signal<AvisoReporte | null>(null);
  protected readonly zipDownloadUrl = signal<string | null>(null);

  ngOnDestroy(): void {
    this.revocarUrlDescarga();
  }

  protected seleccionarArchivos(event: Event): void {
    const input = event.target as HTMLInputElement | null;
    const lista = input?.files ? Array.from(input.files) : [];
    this.archivos.set(lista);
    this.aviso.set(null);
    this.revocarUrlDescarga();
  }

  protected async generarPdfs(): Promise<void> {
    if (this.procesando()) {
      return;
    }

    const seleccionados = this.archivos();
    if (seleccionados.length === 0) {
      this.aviso.set({
        tipo: 'error',
        texto: 'Por favor, selecciona al menos un archivo XML.',
      });
      return;
    }

    this.procesando.set(true);
    this.aviso.set(null);
    this.revocarUrlDescarga();

    try {
      const resultado = await firstValueFrom(this.api.generarPdfs(seleccionados));
      if (typeof URL !== 'undefined' && typeof URL.createObjectURL === 'function') {
        this.zipDownloadUrl.set(URL.createObjectURL(resultado.blob));
      } else {
        this.zipDownloadUrl.set('ReportesGenerados.zip');
      }

      this.aviso.set({
        tipo: 'exito',
        texto: `Se generaron ${resultado.validCount} PDFs de ${resultado.receivedCount} XMLs.`,
      });
    } catch (e) {
      this.aviso.set({
        tipo: 'error',
        texto: await extraerErrorBlob(e),
      });
    } finally {
      this.procesando.set(false);
    }
  }

  private revocarUrlDescarga(): void {
    const actual = this.zipDownloadUrl();
    if (actual && actual.startsWith('blob:') && typeof URL.revokeObjectURL === 'function') {
      URL.revokeObjectURL(actual);
    }
    this.zipDownloadUrl.set(null);
  }
}

async function extraerErrorBlob(error: unknown): Promise<string> {
  if (!(error instanceof HttpErrorResponse)) {
    return 'No se pudieron generar los reportes PDF.';
  }
  if (error.status === 0) {
    return 'Sin conexión con el backend.';
  }
  if (error.error instanceof Blob) {
    try {
      const text = await error.error.text();
      const json = JSON.parse(text) as { detail?: string };
      if (json.detail) {
        return json.detail;
      }
    } catch {
      // Ignorar si no es JSON válido
    }
  }
  const detail = (error.error as { detail?: string } | null)?.detail;
  return detail ?? 'No se subieron archivos XML válidos.';
}
