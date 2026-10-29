# `06_frontend` — App Angular nueva en `/newapp`

Primera pieza del frontend nuevo (*Strangler Fig*): se monta **encima** de las apps legacy sin
reemplazar ninguna. Angular 22 (standalone, zoneless, signals), servida por nginx sin privilegios detrás
del gateway YARP de [`07_reverse/01_deployment`](../07_reverse/01_deployment/README.md).

| Ruta | Acceso | Qué hace |
| :--- | :--- | :--- |
| `/newapp/` | Pública | Presentación con el botón *Entrar*, que lleva al login de Keycloak. |
| `/newapp/menu` | Con sesión | Menú hacia BrokenTaxes (`/taxes`, MVC), Legacy WebForms (`/legacy`) y LEgFrontNEWBACK. |
| `/newapp/legfrontnewback` | Con sesión | Versión Angular de `LEgFrontNEWBACK.aspx`: las 5 tablas de ventas desde `05_backend` (`/api/sales/*`). La página WebForms sigue siendo la oficial. |

`/` del sitio redirige (302) a `/newapp/`. Cualquier otra ruta bajo `/newapp/` vuelve a la presentación.

---

## 1. Estructura

```text
src/
├── main.ts                       # Lee config.json y arranca; si falla muestra el error en la página
├── app/
│   ├── app.config.ts             # Router, HttpClient + interceptor, angular-auth-oidc-client
│   ├── app.routes.ts             # '' pública; menu y legfrontnewback con authGuard
│   ├── core/
│   │   ├── config/app-config.ts  # Contrato y validación de config.json
│   │   └── auth/                 # AuthService, OidcAuthService, guards (sesión y rol), roles.ts, interceptor, MemorySecurityStorage
│   └── features/
│       ├── inicio/               # Presentación
│       ├── menu/                 # Menú (menu-options.ts: opciones y roles que las ven)
│       ├── sin-acceso/           # Con sesión pero sin ningún rol de la app
│       └── legfrontnewback/      # SalesApiService + sales-table (paginado de 50 en el servidor)
public/config.json                # Config para ng serve (en el contenedor se genera al arrancar)
proxy.conf.json                   # ng serve: /api → https://192.168.50.10
docker/                           # nginx, plantillas y script que genera config.json y la CSP
```

## 2. Autenticación

- Cliente **`frontend-spa`** de Keycloak: público, Authorization Code + PKCE (S256), con
  `angular-auth-oidc-client` 22.0.1. El access token trae `aud: backend-api`.
- **Los tokens viven solo en memoria** (`MemorySecurityStorage`). En `sessionStorage` (prefijo
  `newapp.oidc.`) quedan únicamente los datos del flujo que deben sobrevivir al redirect de Keycloak
  (code verifier, state, nonce). Ni `localStorage` ni cookies llevan tokens.
- Al recargar la página la sesión se recupera sin contraseña gracias a la sesión SSO de Keycloak.
  El token se renueva con el refresh token 30 s antes de vencer.
- Keycloak siempre regresa a `/newapp/`. La ruta pedida se guarda en `sessionStorage`
  (`newapp.returnUrl`) y se restaura al volver; por defecto, `/menu`.
- El interceptor pone `Authorization: Bearer` **solo** en las llamadas a `apiBaseUrl`. Ante un 401
  intenta renovar una vez y repite; si no puede, manda al login.
- La defensa principal contra XSS es la CSP del contenedor (`script-src 'self'`, `connect-src` limitado
  a la app, Keycloak y la API).
- SSO con las apps legacy: entrar en `/newapp` abre la sesión de Keycloak, y `/taxes` y `/legacy` entran
  sin contraseña.
- **Logout global:** *Cerrar sesión* cierra la sesión de Keycloak, que a su vez cierra las de `/taxes` y
  `/legacy` (front-channel, ver `07_reverse/01_deployment/README.md`). Al revés, si se sale desde
  `/taxes` o `/legacy`, el *check session* de Keycloak (un iframe que revisa la sesión cada pocos
  segundos) avisa a esta app, que borra sus tokens de memoria y vuelve a la presentación. Solo se activa
  con Keycloak en el mismo origen que la app (`usarCheckSession`): desde `ng serve` el iframe no recibe
  la cookie de Keycloak y la sesión acaba al fallar la siguiente renovación del token.
- **Roles** (`core/auth/roles.ts`, de `realm_access.roles`): `/menu` y `/legfrontnewback` piden
  `usuario`, `admin` o `invitado` (`roleGuard`, después de `authGuard`); sin ninguno, `/sin-acceso`. En el
  menú, `/taxes` y `/legacy` son para `usuario` y `admin`; LEgFrontNEWBACK también para `invitado`.
  Es solo presentación: los datos los protege el backend (política `Lectura` en `/api/sales`).

## 3. Desarrollo local (`ng serve`)

Requisitos: Node 24 y pnpm 10.

```bash
cd 06_frontend
pnpm install
pnpm start          # http://localhost:4200/newapp/
```

- `public/config.json` apunta al Keycloak del laboratorio (`https://192.168.50.10/auth/realms/laboratorio`).
  `localhost:4200` sigue en http: el navegador lo trata como contexto seguro.
- `proxy.conf.json` manda `/api` al laboratorio por https (mismo origen que la app, sin CORS) y valida el
  certificado. Node debe confiar en la CA de mkcert (ver `07_reverse/01_deployment/README.md`, *HTTPS*):

  ```powershell
  $env:NODE_USE_SYSTEM_CA = "1"   # Node 24: usa el almacén de Windows, donde mkcert -install dejó la CA
  # o bien: $env:NODE_EXTRA_CA_CERTS = "$(mkcert -CAROOT)\rootCA.pem"
  ```

  Sin eso falla el proxy de `/api` (`UNABLE_TO_VERIFY_LEAF_SIGNATURE` en la consola de `ng serve`).
- Para iniciar sesión, `frontend-spa` debe aceptar `http://localhost:4200`. Se agrega y se quita con un
  script que solo corre en development (desde `07_reverse/01_deployment`):

  ```bash
  ./keycloak/scripts/frontend-spa-localhost.sh development agregar   # ver | agregar | quitar
  ```

  Ese Keycloak es el del laboratorio: al terminar de desarrollar, `quitar`.

## 4. Pruebas (TDD)

```bash
pnpm test           # Vitest vía ng test
```

Cubren la validación de `config.json`, el guard, el interceptor (Bearer solo a la API, renovación ante
401), el almacenamiento en memoria, el servicio OIDC (ruta de regreso), el menú y las tablas de ventas.
Los componentes se prueban con `FakeAuthService` (`core/auth/testing`).

## 5. Contenedor

Una sola imagen para todos los ambientes: `config.json` y la CSP se generan **al arrancar** desde
variables de entorno (`docker/40-newapp-config.sh`). Si falta una variable, el contenedor no arranca.

| Variable del contenedor | Sale de (`.env.<ambiente>`) | Por defecto |
| :--- | :--- | :--- |
| `NEWAPP_OIDC_AUTHORITY` | `${KC_HOSTNAME}/realms/${KC_REALM}` | — |
| `NEWAPP_OIDC_CLIENT_ID` | `KC_FRONTEND_CLIENT_ID` | `frontend-spa` |
| `NEWAPP_OIDC_SCOPE` | `NEWAPP_OIDC_SCOPE` | `openid profile email` |
| `NEWAPP_API_BASE_URL` | `NEWAPP_API_BASE_URL` | `/api` |
| `NEWAPP_TAXES_URL` | `NEWAPP_TAXES_URL` | `/taxes/Home/Menu` |
| `NEWAPP_LEGACY_URL` | `NEWAPP_LEGACY_URL` | `/legacy/Menu` |

nginx (puerto 8080, usuario `nginx`): `/healthz` para el healthcheck, `config.json` e `index.html` sin
caché, JS/CSS con hash en caché inmutable, y cabeceras de seguridad (CSP, `X-Frame-Options: DENY`,
`nosniff`, `Referrer-Policy`).

```bash
cd 06_frontend
docker compose --env-file ../07_reverse/01_deployment/.env.development up -d --build
```

- Se une a la red externa `backend`; el gateway lo alcanza en `http://frontend-newapp:8080`
  (`FRONTEND_NEWAPP_BASE_URL`).
- Comparte el proyecto compose (`COMPOSE_PROJECT_NAME`, p. ej. `lab-development`) con `07_reverse` y
  `05_backend`. El aviso de *orphan containers* es normal. **Nunca usar `--remove-orphans`**: borraría
  nginx, gateway, Keycloak y `backend-api`.
- Orden al desplegar: primero este contenedor y después el gateway con la ruta `/newapp`. Al revés, `/`
  redirige a una app que no existe y el gateway responde 502 en `/newapp/*` (lo demás sigue funcionando).

Verificación:

```bash
curl -i --cacert rootCA.pem https://192.168.50.10/                   # 302 → /newapp/
curl -i --cacert rootCA.pem https://192.168.50.10/newapp/menu        # 200 (index.html; el guard actúa en el navegador)
curl -s --cacert rootCA.pem https://192.168.50.10/newapp/config.json # config generada al arrancar
```

## 6. Pendientes

- **LEgFrontNEWBACK:** cuando se valide, decidir si reemplaza a la página WebForms.
- **Jenkins:** despliegue de esta imagen.
