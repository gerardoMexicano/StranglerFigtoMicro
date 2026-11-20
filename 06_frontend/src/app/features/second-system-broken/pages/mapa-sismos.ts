import {
  afterNextRender,
  Component,
  computed,
  DestroyRef,
  effect,
  ElementRef,
  inject,
  input,
  viewChild,
} from '@angular/core';
import * as L from 'leaflet';

import { PuntoSismico } from '../sismos-api.service';

/** Centro de México, igual que los mapas de PaginaAngular en MVC. */
const CENTRO: L.LatLngTuple = [23.6345, -102.5528];
const ZOOM = 5;
const COLOR_NET10 = '#10b981';

/**
 * Mapa Leaflet con los sismos que devuelve 05_backend. preferCanvas como en la vista MVC:
 * miles de circleMarker en SVG harían lento el render.
 */
@Component({
  selector: 'app-mapa-sismos',
  template: '<div #contenedor class="mapa" role="img" [attr.aria-label]="descripcion()"></div>',
  styles: `
    .mapa {
      height: 420px;
      width: 100%;
      border: 2px solid var(--color-borde);
      border-radius: var(--radio);
    }
  `,
})
export class MapaSismos {
  readonly puntos = input<readonly PuntoSismico[]>([]);

  private readonly contenedor = viewChild.required<ElementRef<HTMLElement>>('contenedor');
  private mapa?: L.Map;
  private capa?: L.LayerGroup;

  constructor() {
    afterNextRender(() => {
      this.mapa = L.map(this.contenedor().nativeElement, { preferCanvas: true }).setView(
        CENTRO,
        ZOOM,
      );
      L.tileLayer('https://tile.openstreetmap.org/{z}/{x}/{y}.png', {
        attribution: '&copy; OpenStreetMap contributors',
        maxZoom: 19,
        // La app manda Referrer-Policy same-origin; OSM pide al menos el origen para servir teselas
        referrerPolicy: 'strict-origin-when-cross-origin',
      }).addTo(this.mapa);
      this.capa = L.layerGroup().addTo(this.mapa);
      this.pintar(this.puntos());
    });

    effect(() => this.pintar(this.puntos()));

    inject(DestroyRef).onDestroy(() => this.mapa?.remove());
  }

  protected readonly descripcion = computed(() => {
    const total = this.puntos().length;
    return total > 0 ? `Mapa de México con ${total} sismos` : 'Mapa de México sin sismos';
  });

  private pintar(puntos: readonly PuntoSismico[]): void {
    if (!this.capa) {
      return;
    }
    this.capa.clearLayers();
    for (const p of puntos) {
      L.circleMarker([p.lat, p.lng], {
        radius: 3,
        color: COLOR_NET10,
        fillColor: COLOR_NET10,
        weight: 1,
        opacity: 0.8,
        fillOpacity: 0.6,
      }).addTo(this.capa);
    }
  }
}
