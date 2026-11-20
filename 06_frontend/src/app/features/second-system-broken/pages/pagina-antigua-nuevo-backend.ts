import { CurrencyPipe, DatePipe, DecimalPipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';

import {
  ConsolidatedReportApiService,
  ConsolidatedReportQuery,
} from '../consolidated-report-api.service';

/** Como la vista MVC: el concepto se corta a 40 caracteres y el texto completo va en el title. */
const LARGO_CONCEPTO = 40;

/**
 * Versión Angular de /taxes/Home/PaginaAntiguaNuevoBackend: el Reporte Masivo Consolidado
 * paginado en 05_backend (/api/taxes/consolidated-report), ya sin pasar por IIS.
 */
@Component({
  selector: 'app-pagina-antigua-nuevo-backend',
  imports: [CurrencyPipe, DatePipe, DecimalPipe, FormsModule, RouterLink],
  templateUrl: './pagina-antigua-nuevo-backend.html',
  styleUrl: './pagina-antigua-nuevo-backend.css',
})
export class PaginaAntiguaNuevoBackend {
  private readonly api = inject(ConsolidatedReportApiService);

  protected readonly inicio = signal('');
  protected readonly fin = signal('');

  /** null = todavía no se consulta (estado inicial de la vista MVC). */
  protected readonly consulta = signal<ConsolidatedReportQuery | null>(null);

  protected readonly rangoInvalido = computed(
    () => !!this.inicio() && !!this.fin() && this.inicio() > this.fin(),
  );
  protected readonly sinFechas = computed(() => !this.inicio() || !this.fin());

  protected readonly reporte = rxResource({
    params: () => this.consulta() ?? undefined,
    stream: ({ params }) => this.api.getPage(params),
  });

  protected readonly pagina = computed(() =>
    this.reporte.hasValue() ? this.reporte.value() : undefined,
  );

  protected readonly error = computed(() => {
    const error = this.reporte.error();
    return error ? mensajeDeError(error) : null;
  });

  protected consultar(): void {
    if (this.sinFechas() || this.rangoInvalido()) {
      return;
    }
    // Objeto nuevo en cada clic: con las mismas fechas también recarga, siempre desde la página 1
    this.consulta.set({ startDate: this.inicio(), endDate: this.fin(), page: 1 });
  }

  protected limpiar(): void {
    this.inicio.set('');
    this.fin.set('');
    this.consulta.set(null);
  }

  protected irA(page: number): void {
    const actual = this.consulta();
    if (actual) {
      this.consulta.set({ ...actual, page });
    }
  }

  protected concepto(texto: string | null): string {
    const valor = texto ?? '';
    return valor.length > LARGO_CONCEPTO ? `${valor.substring(0, LARGO_CONCEPTO)}...` : valor;
  }
}

function mensajeDeError(error: unknown): string {
  if (!(error instanceof HttpErrorResponse)) {
    return 'No se pudo consultar el reporte.';
  }
  if (error.status === 0) {
    return 'Sin conexión con el backend.';
  }
  if (error.status === 502 || error.status === 503 || error.status === 504) {
    return `El backend no está disponible (${error.status}).`;
  }
  const problema = error.error as { title?: string; detail?: string } | null;
  const texto = problema?.detail ?? problema?.title;
  return texto ? `${texto} (${error.status}).` : `El backend respondió ${error.status}.`;
}
