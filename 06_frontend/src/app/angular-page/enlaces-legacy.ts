/**
 * URLs de WebForms a partir de apps.legacyUrl de config.json (la entrada al menú legacy,
 * p. ej. https://192.168.50.10/legacy/Menu). Así no hace falta otra llave por ambiente.
 */
export interface EnlacesLegacy {
  /** /legacy/: el Inicio y la marca del Site.Master */
  inicio: string;
  /** /legacy/Menu: el Menú del Site.Master */
  menu: string;
  /** /legacy/AngularPage: esta misma app con las rutas de angular-page.routes.ts */
  angularPage: string;
}

export function enlacesLegacy(legacyUrl: string, origen = location.origin): EnlacesLegacy {
  const menu = new URL(legacyUrl, origen);
  return {
    inicio: new URL('./', menu).href,
    menu: menu.href,
    angularPage: new URL('AngularPage', menu).href,
  };
}
