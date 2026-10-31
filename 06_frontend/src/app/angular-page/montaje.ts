import { Routes } from '@angular/router';

import { routes } from '../app.routes';
import { angularPageRoutes } from './angular-page.routes';

/**
 * Dónde vive la app en el navegador. Es un solo build (base href /newapp/, de donde salen js, css
 * y config.json), pero el gateway también la sirve en /legacy/AngularPage con otro juego de rutas.
 */
export interface Montaje {
  /** Ruta base del router; null = la del <base href> (/newapp/). */
  baseHref: string | null;
  routes: Routes;
}

export const MONTAJE_NEWAPP: Montaje = { baseHref: null, routes };

/** legacy-angularpage-route del gateway (07_reverse) y su location en docker/default.conf. */
export const MONTAJE_ANGULAR_PAGE: Montaje = {
  baseHref: '/legacy/AngularPage/',
  routes: angularPageRoutes,
};

/** El montaje según la URL con la que entró el navegador. Sin distinguir mayúsculas, como IIS. */
export function montajePara(pathname: string): Montaje {
  return /^\/legacy\/angularpage(?:\/|$)/i.test(pathname) ? MONTAJE_ANGULAR_PAGE : MONTAJE_NEWAPP;
}
