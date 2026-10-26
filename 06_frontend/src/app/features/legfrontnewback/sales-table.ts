import { DecimalPipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, input, linkedSignal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { map } from 'rxjs';

import { SalesApiService, SalesResource } from './sales-api.service';

export interface RangoFechas {
  /** yyyy-MM-dd */
  startDate: string;
  /** yyyy-MM-dd */
  endDate: string;
}

/**
 * Una tabla de LEgFrontNEWBACK. Carga sola en cuanto recibe un rango, pagina en el servidor
 * (50 filas) y muestra cuánto tardó cada página: las cinco cargan en paralelo, no una tras otra
 * como en WebForms.
 */
@Component({
  selector: 'app-sales-table',
  imports: [DecimalPipe],
  templateUrl: './sales-table.html',
  styleUrl: './sales-table.css',
})
export class SalesTable {
  private readonly api = inject(SalesApiService);

  readonly resource = input.required<SalesResource>();
  readonly title = input.required<string>();
  /** null = todavía no se pide nada. Un rango nuevo recarga desde la página 1. */
  readonly rango = input<RangoFechas | null>(null);

  protected readonly pagina = linkedSignal({ source: this.rango, computation: () => 1 });

  protected readonly datos = rxResource({
    params: () => {
      const rango = this.rango();
      return rango ? { ...rango, page: this.pagina(), resource: this.resource() } : undefined;
    },
    stream: ({ params }) => {
      const inicio = performance.now();
      return this.api
        .getPage(params.resource, params)
        .pipe(map((pagina) => ({ pagina, ms: Math.round(performance.now() - inicio) })));
    },
  });

  protected readonly resultado = computed(() =>
    this.datos.hasValue() ? this.datos.value() : undefined,
  );

  /** Como AutoGenerateColumns: las columnas son las propiedades de la primera fila. */
  protected readonly columnas = computed(() => {
    const filas = this.resultado()?.pagina.items ?? [];
    return filas.length > 0 ? Object.keys(filas[0]) : [];
  });

  protected readonly error = computed(() => {
    const error = this.datos.error();
    return error ? mensajeDeError(error) : null;
  });

  protected anterior(): void {
    this.pagina.update((p) => Math.max(1, p - 1));
  }

  protected siguiente(): void {
    this.pagina.update((p) => p + 1);
  }

  protected celda(valor: unknown): string {
    if (valor === null || valor === undefined) {
      return '';
    }
    return typeof valor === 'object' ? JSON.stringify(valor) : String(valor);
  }
}

function mensajeDeError(error: unknown): string {
  if (!(error instanceof HttpErrorResponse)) {
    return 'No se pudo cargar la tabla.';
  }
  if (error.status === 0) {
    return 'Sin conexión con el backend.';
  }
  if (error.status === 502 || error.status === 503 || error.status === 504) {
    return `El backend no está disponible (${error.status}).`;
  }
  const titulo = (error.error as { title?: string } | null)?.title;
  return titulo ? `${titulo} (${error.status}).` : `El backend respondió ${error.status}.`;
}
