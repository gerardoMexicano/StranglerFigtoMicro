import { Component, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';

import { APP_CONFIG } from '../core/config/app-config';
import { enlacesLegacy } from './enlaces-legacy';

/**
 * El Site.Master de WebForms (barra oscura, Inicio y Menú, pie) con CSS propio: no carga el
 * Bootstrap ni los estilos de IIS. Los enlaces son de página completa porque salen de Angular.
 */
@Component({
  selector: 'app-marco-legacy',
  imports: [RouterOutlet],
  templateUrl: './marco-legacy.html',
  styleUrl: './marco-legacy.css',
})
export class MarcoLegacy {
  protected readonly enlaces = enlacesLegacy(inject(APP_CONFIG).apps.legacyUrl);
  protected readonly anio = new Date().getFullYear();
}
