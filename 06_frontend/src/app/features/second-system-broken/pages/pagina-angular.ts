import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { map } from 'rxjs';

import {
  CONSULTA_SISMOS_POR_DEFECTO,
  ConsultaSismos,
  PuntoSismico,
  SismosApiService,
} from '../sismos-api.service';
import { MapaSismos } from './mapa-sismos';

/**
 * Versión Angular de /taxes/Home/PaginaAngular, solo con el backend .NET 10: la comparativa
 * contra .NET Framework 4.8 se queda en la vista MVC. Las validaciones (1-10000 sismos,
 * magnitud 0-10) las decide 05_backend y aquí solo se muestran.
 */
@Component({
  selector: 'app-pagina-angular',
  imports: [FormsModule, MapaSismos, RouterLink],
  templateUrl: './pagina-angular.html',
  styleUrl: './pagina-angular.css',
})
export class PaginaAngular {
  private readonly api = inject(SismosApiService);

  protected readonly count = signal(CONSULTA_SISMOS_POR_DEFECTO.count);
  protected readonly minMagnitude = signal(CONSULTA_SISMOS_POR_DEFECTO.minMagnitude);
  protected readonly seed = signal(CONSULTA_SISMOS_POR_DEFECTO.seed);

  /** null hasta el primer "Ejecutar"; objeto nuevo en cada clic para repetir la misma consulta. */
  private readonly ejecucion = signal<ConsultaSismos | null>(null);

  protected readonly respuesta = rxResource({
    params: () => this.ejecucion() ?? undefined,
    stream: ({ params }) => {
      const inicio = performance.now();
      return this.api
        .consultar(params)
        .pipe(map((resultado) => ({ resultado, ms: Math.round(performance.now() - inicio) })));
    },
  });

  protected readonly ejecutado = computed(() => this.ejecucion() !== null);

  protected readonly datos = computed(() =>
    this.respuesta.hasValue() ? this.respuesta.value() : undefined,
  );

  protected readonly puntos = computed<readonly PuntoSismico[]>(
    () => this.datos()?.resultado.points ?? [],
  );

  protected readonly error = computed(() => {
    const error = this.respuesta.error();
    return error ? mensajeDeError(error) : null;
  });

  /** Barra de memoria como en MVC: 20 % por MB, entre 5 % y 100 %. */
  protected readonly barraMemoria = computed(() => {
    const mb = this.datos()?.resultado.memoryMegabytes ?? 0;
    return Math.min(100, Math.max(5, Math.round(mb * 20)));
  });

  protected ejecutar(): void {
    this.ejecucion.set({
      count: Number(this.count()),
      minMagnitude: Number(this.minMagnitude()),
      seed: Number(this.seed()),
    });
  }
}

function mensajeDeError(error: unknown): string {
  if (!(error instanceof HttpErrorResponse) || error.status === 0) {
    return 'No se pudo conectar con el servidor.';
  }
  const problema = error.error as { title?: string; detail?: string } | null;
  const texto = problema?.detail ?? problema?.title ?? error.statusText;
  return `HTTP ${error.status}: ${texto}`;
}
