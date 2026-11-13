import { Routes } from '@angular/router';

import { routes } from '../app.routes';
import { angularPageRoutes } from './angular-page.routes';
import { usuariosLegacyRoutes } from './usuarios-legacy.routes';

/**
 * Dónde vive la app en el navegador. Es un solo build (base href /newapp/, de donde salen js, css
 * y config.json), pero el gateway también la sirve en /legacy/AngularPage y /legacy/Usuarios, cada
 * una con su juego de rutas.
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

/** legacy-usuarios-route del gateway (07_reverse) y su location en docker/default.conf. */
export const MONTAJE_USUARIOS: Montaje = {
  baseHref: '/legacy/Usuarios/',
  routes: usuariosLegacyRoutes,
};

/** El montaje según la URL con la que entró el navegador. Sin distinguir mayúsculas, como IIS. */
export function montajePara(pathname: string): Montaje {
  if (/^\/legacy\/angularpage(?:\/|$)/i.test(pathname)) {
    return MONTAJE_ANGULAR_PAGE;
  }
  if (/^\/legacy\/usuarios(?:\/|$)/i.test(pathname)) {
    return MONTAJE_USUARIOS;
  }
  return MONTAJE_NEWAPP;
}
