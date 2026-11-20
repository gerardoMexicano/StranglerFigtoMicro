import { Component, input } from '@angular/core';

import { PuntoSismico } from '../sismos-api.service';

/** Mapa Leaflet con los sismos que devuelve el backend. Fase Red (TDD): solo el contrato. */
@Component({
  selector: 'app-mapa-sismos',
  template: '',
})
export class MapaSismos {
  readonly puntos = input<readonly PuntoSismico[]>([]);
}
