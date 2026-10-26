import { AbstractSecurityStorage } from 'angular-auth-oidc-client';

/**
 * Lo único que se respalda en sessionStorage: el estado del flujo PKCE, que tiene que sobrevivir
 * a la ida y vuelta a Keycloak (página nueva). No son secretos de larga vida: sin el código que
 * Keycloak entrega en el regreso no sirven, y la librería los borra al terminar el login.
 */
const LLAVES_DEL_FLUJO = [
  'codeVerifier',
  'authStateControl',
  'authNonce',
  'storageCodeFlowInProgress',
  'storageCustomParamsAuthRequest',
  'authWellKnownEndPoints',
];

const PREFIJO = 'newapp.oidc.';

/**
 * Almacenamiento de angular-auth-oidc-client. Tokens (access, id, refresh) y datos del usuario
 * viven solo en memoria: nada de eso llega a sessionStorage ni a localStorage. Al recargar la
 * página la sesión se pierde y el guard vuelve a Keycloak, que responde sin pedir contraseña
 * mientras su propia sesión siga viva.
 *
 * La librería guarda un solo JSON por cliente; aquí se parte en dos.
 */
export class MemorySecurityStorage implements AbstractSecurityStorage {
  private readonly memoria = new Map<string, string>();

  constructor(private readonly respaldo: Storage) {}

  read(key: string): string | null {
    return this.memoria.get(key) ?? this.respaldo.getItem(PREFIJO + key);
  }

  write(key: string, value: string): void {
    this.memoria.set(key, value);

    const flujo = soloElFlujo(value);
    if (flujo) {
      this.respaldo.setItem(PREFIJO + key, flujo);
    } else {
      this.respaldo.removeItem(PREFIJO + key);
    }
  }

  remove(key: string): void {
    this.memoria.delete(key);
    this.respaldo.removeItem(PREFIJO + key);
  }

  clear(): void {
    for (const key of [...this.memoria.keys()]) {
      this.remove(key);
    }
    for (const llave of Object.keys(this.respaldo).filter((k) => k.startsWith(PREFIJO))) {
      this.respaldo.removeItem(llave);
    }
  }
}

/** JSON solo con las llaves del flujo, o null si no hay ninguna. */
function soloElFlujo(value: string): string | null {
  let datos: unknown;
  try {
    datos = JSON.parse(value);
  } catch {
    return null;
  }
  if (typeof datos !== 'object' || datos === null) {
    return null;
  }

  const flujo = Object.fromEntries(
    Object.entries(datos).filter(([llave]) => LLAVES_DEL_FLUJO.includes(llave)),
  );
  return Object.keys(flujo).length > 0 ? JSON.stringify(flujo) : null;
}
