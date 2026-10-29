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

import { Ubicacion } from './geolocalizacion';

/** Centro de México, como el mapa de Usuarios.aspx antes de ubicar a nadie. */
const CENTRO_INICIAL: L.LatLngTuple = [23.6345, -102.5528];
const ZOOM_INICIAL = 5;
const ZOOM_UBICADO = 16;

/**
 * El mapa Leaflet de Usuarios.aspx. Marca la ubicación con un círculo del tamaño de la precisión;
 * circleMarker en vez del marcador con imagen: no hay íconos que copiar a public/.
 */
@Component({
  selector: 'app-mapa-asistencia',
  template: '<div #contenedor class="mapa" role="img" [attr.aria-label]="descripcion()"></div>',
  styles: `
    .mapa {
      height: 400px;
      width: 100%;
      border: 2px solid var(--color-borde);
      border-radius: var(--radio);
    }
  `,
})
export class MapaAsistencia {
  readonly ubicacion = input<Ubicacion | null>(null);

  private readonly contenedor = viewChild.required<ElementRef<HTMLElement>>('contenedor');
  private mapa?: L.Map;
  private capaUbicacion?: L.LayerGroup;

  constructor() {
    afterNextRender(() => {
      this.mapa = L.map(this.contenedor().nativeElement).setView(CENTRO_INICIAL, ZOOM_INICIAL);
      L.tileLayer('https://tile.openstreetmap.org/{z}/{x}/{y}.png', {
        attribution: '&copy; OpenStreetMap contributors',
        maxZoom: 19,
        // La app manda Referrer-Policy same-origin; OSM pide al menos el origen para servir teselas
        referrerPolicy: 'strict-origin-when-cross-origin',
      }).addTo(this.mapa);
      this.capaUbicacion = L.layerGroup().addTo(this.mapa);
      this.pintar(this.ubicacion());
    });

    effect(() => this.pintar(this.ubicacion()));

    inject(DestroyRef).onDestroy(() => this.mapa?.remove());
  }

  protected readonly descripcion = computed(() => {
    const u = this.ubicacion();
    return u
      ? `Mapa con tu ubicación: ${u.latitud.toFixed(6)}, ${u.longitud.toFixed(6)}`
      : 'Mapa de México; todavía no hay ubicación';
  });

  private pintar(ubicacion: Ubicacion | null): void {
    if (!this.mapa || !this.capaUbicacion || !ubicacion) {
      return;
    }
    const centro: L.LatLngTuple = [ubicacion.latitud, ubicacion.longitud];

    this.capaUbicacion.clearLayers();
    L.circle(centro, {
      radius: ubicacion.precisionMetros,
      color: '#4caf50',
      fillOpacity: 0.1,
    }).addTo(this.capaUbicacion);
    L.circleMarker(centro, { radius: 8, color: '#16213e', fillColor: '#4caf50', fillOpacity: 1 })
      .bindPopup('Tu ubicación actual')
      .addTo(this.capaUbicacion)
      .openPopup();
    this.mapa.setView(centro, ZOOM_UBICADO);
  }
}
