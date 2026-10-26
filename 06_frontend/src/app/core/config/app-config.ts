import { InjectionToken } from '@angular/core';

/**
 * Contenido de /newapp/config.json. El contenedor lo genera al arrancar con las variables
 * del ambiente: la misma imagen sirve en development, testing y production.
 */
export interface AppConfig {
  oidc: {
    /** Issuer público del realm, p. ej. http://192.168.50.10/auth/realms/laboratorio */
    authority: string;
    clientId: string;
    scope: string;
  };
  /** Prefijo de 05_backend por el gateway, p. ej. /api */
  apiBaseUrl: string;
  /** Entradas de las apps legacy; el menú solo redirige a ellas. */
  apps: {
    taxesUrl: string;
    legacyUrl: string;
  };
}

export const APP_CONFIG = new InjectionToken<AppConfig>('APP_CONFIG');

export class AppConfigError extends Error {
  override name = 'AppConfigError';
}

const OBLIGATORIAS = [
  'oidc.authority',
  'oidc.clientId',
  'apiBaseUrl',
  'apps.taxesUrl',
  'apps.legacyUrl',
] as const;

const SCOPE_POR_DEFECTO = 'openid profile';

/** Valida el JSON ya leído. Lanza AppConfigError con todas las llaves que faltan. */
export function parseAppConfig(raw: unknown): AppConfig {
  if (!esObjeto(raw)) {
    throw new AppConfigError('config.json debe ser un objeto JSON');
  }

  const faltan = OBLIGATORIAS.filter((ruta) => leerTexto(raw, ruta) === undefined);
  if (faltan.length > 0) {
    throw new AppConfigError(`config.json incompleto: faltan ${faltan.join(', ')}`);
  }

  const valor = (ruta: (typeof OBLIGATORIAS)[number]) => leerTexto(raw, ruta) as string;
  return {
    oidc: {
      authority: valor('oidc.authority'),
      clientId: valor('oidc.clientId'),
      scope: leerTexto(raw, 'oidc.scope') ?? SCOPE_POR_DEFECTO,
    },
    apiBaseUrl: valor('apiBaseUrl'),
    apps: {
      taxesUrl: valor('apps.taxesUrl'),
      legacyUrl: valor('apps.legacyUrl'),
    },
  };
}

/** Lee y valida config.json antes de arrancar Angular. */
export async function loadAppConfig(
  url: string,
  fetchFn: typeof fetch = (entrada, opciones) => fetch(entrada, opciones),
): Promise<AppConfig> {
  let respuesta: Response;
  try {
    // Cambia en cada despliegue sin cambiar de nombre: nunca del caché
    respuesta = await fetchFn(url, { cache: 'no-store' });
  } catch (e) {
    throw new AppConfigError(`No se pudo leer ${url}: ${(e as Error).message}`);
  }

  if (!respuesta.ok) {
    throw new AppConfigError(`No se pudo leer ${url} (HTTP ${respuesta.status})`);
  }

  let json: unknown;
  try {
    json = await respuesta.json();
  } catch {
    throw new AppConfigError(`${url} no es JSON válido`);
  }

  return parseAppConfig(json);
}

function esObjeto(valor: unknown): valor is Record<string, unknown> {
  return typeof valor === 'object' && valor !== null && !Array.isArray(valor);
}

/** Texto en la ruta "a.b" o undefined si no existe o está vacío (envsubst deja "" sin la variable). */
function leerTexto(raiz: Record<string, unknown>, ruta: string): string | undefined {
  const valor = ruta
    .split('.')
    .reduce<unknown>((actual, llave) => (esObjeto(actual) ? actual[llave] : undefined), raiz);
  return typeof valor === 'string' && valor.trim() !== '' ? valor.trim() : undefined;
}
