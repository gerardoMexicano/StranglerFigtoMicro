import { CurrencyPipe, DatePipe, DecimalPipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { rxResource, toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';

import { AuthService } from '../../../core/auth/auth.service';
import { ROLES_ESCRITURA } from '../../../core/auth/roles';
import { NominaApiService } from './nomina-api.service';

export interface AvisoNomina {
  tipo: 'exito' | 'error' | 'info';
  texto: string;
}

interface FiltroFechas {
  startDate?: string;
  endDate?: string;
}

/**
 * B1 — Página Antigua (Dashboard Nómina): migración a Angular de /taxes/PaginaAntigua/Index.
 * Permite cargar hasta 5,000 XMLs CFDI 4.0 de nómina a la BD (CfdiCore, CfdiComplemento,
 * NominaResumen y NominaMovimiento) vía POST /api/taxes/xml-batch y consultar los KPIs,
 * costo por departamento y tabla de recibos vía GET /api/taxes/payroll-summary.
 */
@Component({
  selector: 'app-pagina-antigua',
  imports: [CurrencyPipe, DatePipe, DecimalPipe, FormsModule, RouterLink],
  template: `
    <main class="pagina">
      <div class="barra-superior">
        <a class="boton boton-texto" routerLink="/second-system-broken"
          >← Volver a Second System Broken</a
        >
      </div>

      <header class="encabezado-dashboard">
        <div>
          <h1>Página Antigua</h1>
          <p class="subtitulo">
            Análisis de Nómina y Carga Masiva de XMLs CFDI 4.0 conectado a 05_backend (.NET 10)
          </p>
        </div>
      </header>

      @if (aviso(); as msg) {
        <div
          class="aviso"
          [class.aviso-exito]="msg.tipo === 'exito'"
          [class.aviso-error]="msg.tipo === 'error'"
          [class.aviso-info]="msg.tipo === 'info'"
          role="alert"
        >
          {{ msg.texto }}
        </div>
      }

      <section class="panel panel-carga" aria-labelledby="titulo-carga">
        <div class="panel-encabezado">
          <div>
            <h2 id="titulo-carga">Cargar XMLs de Nómina a Base de Datos</h2>
            <p class="ayuda">
              Selecciona hasta 5,000 archivos XML CFDI 4.0 (inserción en CfdiCore, CfdiComplemento,
              NominaResumen y NominaMovimiento).
            </p>
          </div>
        </div>

        @if (!puedeCargar()) {
          <p class="nota-rol">
            Tu rol es de solo consulta (<code>invitado</code>): puedes filtrar y explorar el
            dashboard, pero la carga masiva de XMLs requiere rol <code>usuario</code> o
            <code>admin</code>.
          </p>
        } @else {
          <div class="controles-carga">
            <input
              id="xmlFilesInput"
              type="file"
              multiple
              accept=".xml"
              (change)="seleccionarArchivos($event)"
              [disabled]="cargandoXml()"
            />
            <button
              type="button"
              class="boton boton-primario"
              id="btnProcesarXml"
              (click)="procesarXmls()"
              [disabled]="cargandoXml() || archivosSeleccionados().length === 0"
            >
              {{
                cargandoXml()
                  ? 'Procesando XMLs...'
                  : 'Procesar XMLs (' + archivosSeleccionados().length + ')'
              }}
            </button>
          </div>
        }
      </section>

      <section class="panel panel-filtros" aria-labelledby="titulo-filtros">
        <h2 id="titulo-filtros" class="sr-only">Filtro de Fechas</h2>
        <form class="formulario-fechas" (ngSubmit)="filtrar()">
          <label class="campo">
            <span>Fecha Inicio</span>
            <input
              id="fechaInicio"
              type="date"
              name="fechaInicio"
              [ngModel]="fechaInicio()"
              (ngModelChange)="fechaInicio.set($event)"
            />
          </label>
          <label class="campo">
            <span>Fecha Fin</span>
            <input
              id="fechaFin"
              type="date"
              name="fechaFin"
              [ngModel]="fechaFin()"
              (ngModelChange)="fechaFin.set($event)"
            />
          </label>
          <div class="acciones-filtro">
            <button type="submit" class="boton boton-secundario" id="btnFiltrar">
              Filtrar Reporte
            </button>
            <button type="button" class="boton boton-texto" id="btnLimpiar" (click)="limpiarFiltro()">
              Ver Todos
            </button>
          </div>
        </form>
      </section>

      @if (resumen.isLoading()) {
        <p class="estado-carga">Consultando resumen de nómina...</p>
      } @else if (resumen.error()) {
        <div class="aviso aviso-error" role="alert">
          No se pudo consultar el reporte de nómina desde el backend.
        </div>
      } @else if (resumen.hasValue()) {
        @let datos = resumen.value();

        <section class="kpis-grid" aria-label="Indicadores clave de nómina">
          <article class="kpi-card">
            <span class="kpi-titulo">Total Percepciones</span>
            <strong class="kpi-valor percepcion">{{
              datos.granTotalPercepciones | currency: 'MXN' : 'symbol-narrow' : '1.2-2'
            }}</strong>
          </article>
          <article class="kpi-card">
            <span class="kpi-titulo">Total Deducciones</span>
            <strong class="kpi-valor deduccion">{{
              datos.granTotalDeducciones | currency: 'MXN' : 'symbol-narrow' : '1.2-2'
            }}</strong>
          </article>
          <article class="kpi-card">
            <span class="kpi-titulo">Gran Total Neto</span>
            <strong class="kpi-valor neto">{{
              datos.granTotalNeto | currency: 'MXN' : 'symbol-narrow' : '1.2-2'
            }}</strong>
          </article>
          <article class="kpi-card">
            <span class="kpi-titulo">Total Recibos</span>
            <strong class="kpi-valor recibos">{{ datos.totalRecibos | number }}</strong>
          </article>
        </section>

        <section class="panel" aria-labelledby="titulo-deptos">
          <h2 id="titulo-deptos">Costo por Departamento</h2>
          @if (datos.costoPorDepartamento.length === 0) {
            <p class="vacio">Sin datos por departamento para el rango seleccionado.</p>
          } @else {
            <ul class="lista-departamentos">
              @for (depto of datos.costoPorDepartamento; track depto.departamento) {
                <li class="fila-depto">
                  <span class="depto-nombre">{{ depto.departamento }}</span>
                  <span class="depto-monto">{{
                    depto.totalPercepciones | currency: 'MXN' : 'symbol-narrow' : '1.2-2'
                  }}</span>
                </li>
              }
            </ul>
          }
        </section>

        <section class="panel" aria-labelledby="titulo-recibos">
          <h2 id="titulo-recibos">Detalle de Recibos</h2>
          <div class="tabla-contenedor">
            <table class="tabla-nomina">
              <thead>
                <tr>
                  <th>Fecha Pago</th>
                  <th>Empleado</th>
                  <th>Departamento</th>
                  <th>Puesto</th>
                  <th class="derecha">Percepciones</th>
                  <th class="derecha">Deducciones</th>
                  <th class="derecha">Neto</th>
                </tr>
              </thead>
              <tbody>
                @for (item of datos.nominas; track item.uuid) {
                  <tr>
                    <td>{{ item.fechaPago ? (item.fechaPago | date: 'dd/MM/yyyy') : '-' }}</td>
                    <td>
                      <div class="emp-nombre">{{ item.receptorNombre || '-' }}</div>
                      <div class="emp-meta">
                        Num: {{ item.numEmpleado || '-' }} | RFC: {{ item.receptorRFC }}
                      </div>
                    </td>
                    <td>{{ item.departamento || '-' }}</td>
                    <td>{{ item.puesto || '-' }}</td>
                    <td class="derecha percepcion">
                      {{ item.totalPercepciones | currency: 'MXN' : 'symbol-narrow' : '1.2-2' }}
                    </td>
                    <td class="derecha deduccion">
                      {{ item.totalDeducciones | currency: 'MXN' : 'symbol-narrow' : '1.2-2' }}
                    </td>
                    <td class="derecha neto">
                      {{ item.total | currency: 'MXN' : 'symbol-narrow' : '1.2-2' }}
                    </td>
                  </tr>
                } @empty {
                  <tr>
                    <td colspan="7" class="vacio">No hay registros de nómina para mostrar.</td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        </section>
      }
    </main>
  `,
  styles: `
    .pagina {
      max-width: 1120px;
      margin: 0 auto;
      padding: 1.5rem 1rem 3rem;
    }
    .encabezado-dashboard {
      margin-bottom: 1.25rem;
    }
    .subtitulo {
      color: #475569;
      margin-top: 0.25rem;
    }
    .panel {
      background: #ffffff;
      border: 1px solid #e2e8f0;
      border-radius: 12px;
      padding: 1.25rem;
      margin-bottom: 1.25rem;
      box-shadow: 0 1px 3px rgba(15, 23, 42, 0.05);
    }
    .panel h2 {
      font-size: 1.15rem;
      margin: 0 0 0.5rem;
      color: #0f172a;
    }
    .ayuda {
      color: #64748b;
      font-size: 0.9rem;
      margin: 0 0 0.75rem;
    }
    .controles-carga {
      display: flex;
      flex-wrap: wrap;
      gap: 0.75rem;
      align-items: center;
    }
    .formulario-fechas {
      display: flex;
      flex-wrap: wrap;
      gap: 1rem;
      align-items: flex-end;
    }
    .campo {
      display: flex;
      flex-direction: column;
      gap: 0.25rem;
      font-size: 0.85rem;
      font-weight: 600;
      color: #334155;
    }
    .campo input {
      padding: 0.45rem 0.65rem;
      border: 1px solid #cbd5e1;
      border-radius: 6px;
      font-size: 0.95rem;
    }
    .acciones-filtro {
      display: flex;
      gap: 0.5rem;
      align-items: center;
    }
    .kpis-grid {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(210px, 1fr));
      gap: 1rem;
      margin-bottom: 1.25rem;
    }
    .kpi-card {
      background: #0f172a;
      color: #f8fafc;
      padding: 1.25rem;
      border-radius: 12px;
      display: flex;
      flex-direction: column;
      gap: 0.4rem;
    }
    .kpi-titulo {
      font-size: 0.8rem;
      text-transform: uppercase;
      letter-spacing: 0.05em;
      color: #94a3b8;
    }
    .kpi-valor {
      font-size: 1.6rem;
      font-weight: 700;
    }
    .percepcion {
      color: #10b981;
    }
    .deduccion {
      color: #f43f5e;
    }
    .neto {
      color: #0ea5e9;
    }
    .recibos {
      color: #f59e0b;
    }
    .lista-departamentos {
      list-style: none;
      padding: 0;
      margin: 0;
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(240px, 1fr));
      gap: 0.75rem;
    }
    .fila-depto {
      display: flex;
      justify-content: space-between;
      padding: 0.65rem 0.9rem;
      background: #f8fafc;
      border: 1px solid #e2e8f0;
      border-radius: 8px;
      font-weight: 600;
    }
    .tabla-contenedor {
      overflow-x: auto;
    }
    .tabla-nomina {
      width: 100%;
      border-collapse: collapse;
      font-size: 0.92rem;
    }
    .tabla-nomina th,
    .tabla-nomina td {
      padding: 0.75rem;
      border-bottom: 1px solid #e2e8f0;
      text-align: left;
    }
    .tabla-nomina th {
      font-size: 0.78rem;
      text-transform: uppercase;
      color: #64748b;
    }
    .tabla-nomina .derecha {
      text-align: right;
    }
    .emp-nombre {
      font-weight: 600;
      color: #0f172a;
    }
    .emp-meta {
      font-size: 0.78rem;
      color: #64748b;
    }
    .aviso {
      padding: 0.85rem 1rem;
      border-radius: 8px;
      margin-bottom: 1rem;
      font-weight: 500;
    }
    .aviso-exito {
      background: #ecfdf5;
      color: #065f46;
      border: 1px solid #a7f3d0;
    }
    .aviso-error {
      background: #fef2f2;
      color: #991b1b;
      border: 1px solid #fecaca;
    }
    .aviso-info {
      background: #eff6ff;
      color: #1e40af;
      border: 1px solid #bfdbfe;
    }
    .vacio {
      text-align: center;
      color: #64748b;
      padding: 1.25rem;
    }
    .nota-rol {
      color: #475569;
      background: #f8fafc;
      padding: 0.75rem;
      border-radius: 8px;
      margin: 0;
    }
  `,
})
export class PaginaAntigua {
  private readonly api = inject(NominaApiService);
  private readonly usuario = toSignal(inject(AuthService).user$, { initialValue: null });

  protected readonly puedeCargar = computed(
    () => this.usuario()?.roles.some((rol) => ROLES_ESCRITURA.includes(rol)) ?? false,
  );

  protected readonly fechaInicio = signal('');
  protected readonly fechaFin = signal('');
  protected readonly filtroActivo = signal<FiltroFechas>({});

  protected readonly archivosSeleccionados = signal<readonly File[]>([]);
  protected readonly cargandoXml = signal(false);
  protected readonly aviso = signal<AvisoNomina | null>(null);

  protected readonly resumen = rxResource({
    params: () => this.filtroActivo(),
    stream: ({ params }) => this.api.obtenerResumen(params.startDate, params.endDate),
  });

  protected seleccionarArchivos(event: Event): void {
    const input = event.target as HTMLInputElement | null;
    const files = input?.files ? Array.from(input.files) : [];
    this.archivosSeleccionados.set(files);
  }

  protected filtrar(): void {
    const inicio = this.fechaInicio().trim();
    const fin = this.fechaFin().trim();
    if (inicio && fin && inicio > fin) {
      this.aviso.set({
        tipo: 'error',
        texto: 'La fecha de inicio no puede ser mayor que la fecha final.',
      });
      return;
    }
    this.aviso.set(null);
    this.filtroActivo.set({
      startDate: inicio || undefined,
      endDate: fin || undefined,
    });
  }

  protected limpiarFiltro(): void {
    this.fechaInicio.set('');
    this.fechaFin.set('');
    this.aviso.set(null);
    this.filtroActivo.set({});
  }

  protected async procesarXmls(): Promise<void> {
    const archivos = this.archivosSeleccionados();
    if (this.cargandoXml() || archivos.length === 0) {
      return;
    }

    if (archivos.length > 5000) {
      this.aviso.set({
        tipo: 'error',
        texto: 'Selecciona máximo 5,000 archivos XML por lote.',
      });
      return;
    }

    this.cargandoXml.set(true);
    this.aviso.set(null);

    try {
      const res = await firstValueFrom(this.api.cargarXmls(archivos));
      this.archivosSeleccionados.set([]);
      this.aviso.set({
        tipo: 'exito',
        texto: `Carga con éxito: ${res.message}`,
      });
      this.resumen.reload();
    } catch (e) {
      this.aviso.set({
        tipo: 'error',
        texto: mensajeErrorCarga(e),
      });
    } finally {
      this.cargandoXml.set(false);
    }
  }
}

function mensajeErrorCarga(error: unknown): string {
  if (!(error instanceof HttpErrorResponse)) {
    return 'Error al procesar los archivos XML.';
  }
  if (error.status === 0) {
    return 'Sin conexión con el backend.';
  }
  if (error.status === 403) {
    return 'Tu rol no tiene permisos para cargar archivos XML.';
  }
  const detalle = (error.error as { detail?: string } | null)?.detail;
  return detalle ?? `El backend respondió ${error.status} al procesar los XMLs.`;
}
