import { MemorySecurityStorage } from './memory-security-storage';

// Así guarda angular-auth-oidc-client: un solo JSON por cliente (llave = configId)
const CONFIG_ID = '0-frontend-spa';

const antesDeIrAKeycloak = {
  codeVerifier: 'verificador-pkce',
  authStateControl: 'estado',
  authNonce: 'nonce',
  storageCodeFlowInProgress: true,
  authWellKnownEndPoints: { tokenEndpoint: 'http://kc/token' },
};

const conSesion = {
  ...antesDeIrAKeycloak,
  authzData: 'access-token',
  authnResult: { id_token: 'id-token', refresh_token: 'refresh-token' },
  reusable_refresh_token: 'refresh-token',
  userData: { preferred_username: 'juan' },
  access_token_expires_at: 1234567890,
  session_state: 'abc',
};

describe('MemorySecurityStorage', () => {
  beforeEach(() => sessionStorage.clear());

  it('lee lo que escribió', () => {
    const storage = new MemorySecurityStorage(sessionStorage);

    storage.write(CONFIG_ID, JSON.stringify(conSesion));

    expect(JSON.parse(storage.read(CONFIG_ID) as string)).toEqual(conSesion);
  });

  it('sin nada escrito devuelve null', () => {
    expect(new MemorySecurityStorage(sessionStorage).read(CONFIG_ID)).toBeNull();
  });

  it('ningún token ni dato del usuario llega a sessionStorage', () => {
    new MemorySecurityStorage(sessionStorage).write(CONFIG_ID, JSON.stringify(conSesion));

    const guardado = Object.values({ ...sessionStorage }).join(' ');
    for (const secreto of ['access-token', 'id-token', 'refresh-token', 'juan', 'abc']) {
      expect(guardado).not.toContain(secreto);
    }
  });

  it('nada llega a localStorage', () => {
    localStorage.clear();
    new MemorySecurityStorage(sessionStorage).write(CONFIG_ID, JSON.stringify(conSesion));

    expect(localStorage.length).toBe(0);
  });

  it('el estado del flujo PKCE sobrevive a la redirección a Keycloak (página nueva)', () => {
    new MemorySecurityStorage(sessionStorage).write(CONFIG_ID, JSON.stringify(antesDeIrAKeycloak));

    const alRegresar = new MemorySecurityStorage(sessionStorage);

    expect(JSON.parse(alRegresar.read(CONFIG_ID) as string)).toEqual(antesDeIrAKeycloak);
  });

  it('al recargar la página la sesión se pierde: solo queda el estado del flujo', () => {
    new MemorySecurityStorage(sessionStorage).write(CONFIG_ID, JSON.stringify(conSesion));

    const recargada = JSON.parse(
      new MemorySecurityStorage(sessionStorage).read(CONFIG_ID) as string,
    );

    expect(recargada).toEqual(antesDeIrAKeycloak);
    expect(recargada.authzData).toBeUndefined();
  });

  it('remove borra la memoria y sessionStorage', () => {
    const storage = new MemorySecurityStorage(sessionStorage);
    storage.write(CONFIG_ID, JSON.stringify(conSesion));

    storage.remove(CONFIG_ID);

    expect(storage.read(CONFIG_ID)).toBeNull();
    expect(new MemorySecurityStorage(sessionStorage).read(CONFIG_ID)).toBeNull();
  });

  it('clear borra lo suyo y no toca otras llaves de sessionStorage', () => {
    sessionStorage.setItem('otra-app', 'x');
    const storage = new MemorySecurityStorage(sessionStorage);
    storage.write(CONFIG_ID, JSON.stringify(conSesion));

    storage.clear();

    expect(storage.read(CONFIG_ID)).toBeNull();
    expect(new MemorySecurityStorage(sessionStorage).read(CONFIG_ID)).toBeNull();
    expect(sessionStorage.getItem('otra-app')).toBe('x');
  });

  it('acepta el "null" que escribe la librería al vaciar', () => {
    const storage = new MemorySecurityStorage(sessionStorage);

    storage.write(CONFIG_ID, 'null');

    expect(storage.read(CONFIG_ID)).toBe('null');
    expect(new MemorySecurityStorage(sessionStorage).read(CONFIG_ID)).toBeNull();
  });
});
