# `06_frontend` — App Angular nueva en `/newapp`

Primera pieza del frontend nuevo (*Strangler Fig*): se monta **encima** de las apps legacy sin
reemplazar ninguna. Angular 22 (standalone, zoneless, signals), servida por nginx sin privilegios detrás
del gateway YARP de [`07_reverse/01_deployment`](../07_reverse/01_deployment/README.md).

| Ruta | Acceso | Qué hace |
| :--- | :--- | :--- |
| `/newapp/` | Pública | Presentación con el botón *Entrar*, que lleva al login de Keycloak. |
| `/newapp/menu` | Con sesión | Menú de bienvenida en Angular con acceso a los dos submenús: **System Principal** (`/newapp/system-principal`) y **Second System Broken** (`/newapp/second-system-broken`). |
| `/newapp/system-principal` | Con sesión | Menú Angular que reemplaza al menú principal anterior, con sus 7 accesos: Reportes, Usuarios (`/newapp/usuarios`), Configuración, Dashboard, Second System Broken (`/newapp/second-system-broken`), Reservación de salas (`/newapp/salas`) y LEgFrontNEWBACK (`/newapp/legfrontnewback`). |
| `/newapp/system-principal/reportes` | Con sesión | **L2 — Reportes:** carga uno o varios archivos XML CFDI 4.0 a `05_backend` (`POST /api/reports/cfdi-pdf-batch`) y descarga el archivo `ReportesGenerados.zip` con un PDF (`Reporte_<UUID>.pdf`) por cada factura válida. |
| `/newapp/system-principal/configuracion` | Con sesión | **L3 — Configuración:** panel de perfil e identidad OIDC validado contra `05_backend` (`GET /api/system/me`), matriz de permisos por rol de Keycloak (`admin`, `usuario`, `invitado`), información del entorno (`config.json`) y preferencias visuales locales en el navegador. |
| `/newapp/system-principal/dashboard` | Con sesión | Hoja vacía en Angular para Dashboard. |
| `/newapp/second-system-broken` | Con sesión | Menú Angular que reemplaza al segundo menú anterior, con sus 4 accesos: Página Antigua, Página Antigua (Nuevo Backend), Página en Angular y Página Principal (`/newapp/system-principal`). |
| `/newapp/second-system-broken/pagina-antigua` | Con sesión | **B1 — Página Antigua (Dashboard Nómina):** carga masiva de hasta 5,000 archivos XML CFDI 4.0 de nómina a la BD (`POST /api/taxes/xml-batch`, roles `usuario` y `admin`) y dashboard con filtro de fechas, KPIs, costo por departamento y tabla de recibos (`GET /api/taxes/payroll-summary`). |
| `/newapp/second-system-broken/pagina-antigua-nuevo-backend` | Con sesión | Hoja vacía en Angular para Página Antigua (Nuevo Backend). |
| `/newapp/second-system-broken/pagina-angular` | Con sesión | Hoja vacía en Angular para Página en Angular. |
| `/newapp/legfrontnewback` | Con sesión | Versión Angular de `LEgFrontNEWBACK.aspx`: las 5 tablas de ventas desde `05_backend` (`/api/sales/*`). |
| `/newapp/usuarios` | Con sesión | Checador de Asistencia. **Reemplaza** a `Usuarios.aspx` (migración completa): ubica al usuario en un mapa Leaflet, registra el check-in en `05_backend` (`/api/attendance/check-ins`) y muestra sus últimos registros. `invitado` ve su ubicación pero no registra. |
| `/newapp/salas` | Con sesión | Reservación de salas accesible directamente desde `System Principal` en `/newapp/salas`. |
| `/legacy/Usuarios` | Con sesión | El mismo Checador de Asistencia en la URL de siempre: el gateway sirve esta app en lugar de `Usuarios.aspx` (`legacy-usuarios-route`), dentro del marco de WebForms. `/legacy/Usuarios.aspx` redirige (302) aquí. |

| `/legacy/AngularPage` | Con sesión | Reservación de salas (funcionalidad nueva). Es **esta misma app**, pero el gateway la sirve en lugar de `AngularPage.aspx` dentro de un marco igual al `Site.Master` de WebForms: para el usuario sigue siendo una página de `/legacy`. Agenda del día por sala y reservación en bloques de 30 minutos contra `05_backend` (`/api/rooms`). `invitado` consulta pero no reserva. |

`/` del sitio redirige (302) a `/newapp/`. Cualquier otra ruta bajo `/newapp/` vuelve a la presentación.

### Montaje en `/legacy/AngularPage`

Un solo build y una sola imagen. `main.ts` mira la URL (`montajePara` en `app/angular-page/montaje.ts`):

- Bajo `/legacy/AngularPage` (sin distinguir mayúsculas) arranca con `APP_BASE_HREF=/legacy/AngularPage/` y las
  rutas de `angular-page.routes.ts`: la raíz es la Reservación de salas dentro de `MarcoLegacy` (barra
  *Sistema Laboratorio*, *Inicio*, *Menú* y pie, con CSS propio: no carga Bootstrap ni nada de IIS).
- El `<base href>` sigue siendo `/newapp/`: js, css y `config.json` se piden ahí. nginx devuelve el mismo
  `index.html` para `/legacy/AngularPage*` (`docker/default.conf`).
- Keycloak regresa a `/legacy/AngularPage/`, que ya cubre `${APP_PUBLIC_URL}/*` de `frontend-spa`. Con la sesión
  SSO de WebForms el salto a `/auth` no pide contraseña.
- Los enlaces de WebForms (*Inicio*, *Menú*, la tarjeta del menú de `/newapp`) salen de `apps.legacyUrl`: no hay
  llave nueva en `config.json`.
- El resto de `/newapp` no cambia. La ruta del gateway y el rollback están en
  [`07_reverse/01_deployment/README.md`](../07_reverse/01_deployment/README.md#app-nueva-newapp-06_frontend).

---

## 1. Estructura

```text
src/
├── main.ts                       # Lee config.json y arranca; si falla muestra el error en la página
├── app/
│   ├── app.config.ts             # Router (según el montaje), HttpClient + interceptor, angular-auth-oidc-client
│   ├── app.routes.ts             # '' pública; menu, legfrontnewback y usuarios con authGuard
│   ├── angular-page/             # Montaje en /legacy/AngularPage: rutas, MarcoLegacy (Site.Master) y enlaces a WebForms
│   ├── core/
│   │   ├── config/app-config.ts  # Contrato y validación de config.json
│   │   └── auth/                 # AuthService, OidcAuthService, guards (sesión y rol), roles.ts, interceptor, MemorySecurityStorage
│   └── features/
│       ├── inicio/               # Presentación
│       ├── menu/                 # Menú (menu-options.ts: opciones y roles que las ven)
│       ├── sin-acceso/           # Con sesión pero sin ningún rol de la app
│       ├── legfrontnewback/      # SalesApiService + sales-table (paginado de 50 en el servidor)
│       ├── usuarios/             # Checador de Asistencia: AsistenciaApiService, Geolocalizacion, mapa Leaflet
│       └── salas/                # Reservación de salas: SalasApiService, agenda.ts (bloques y tabla del día)
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
- **Roles** (`core/auth/roles.ts`, de `realm_access.roles`): `/menu`, `/legfrontnewback` y `/usuarios`
  piden `usuario`, `admin` o `invitado` (`roleGuard`, después de `authGuard`); sin ninguno, `/sin-acceso`.
  En el menú, `/taxes` y `/legacy` son para `usuario` y `admin`; LEgFrontNEWBACK y Usuarios también para
  `invitado`. En `/usuarios` solo `usuario` y `admin` registran asistencia (`ROLES_ESCRITURA`). Es solo
  presentación: los datos los protege el backend (políticas `Lectura` y `UsuarioOrAdmin`).

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
`nosniff`, `Referrer-Policy`, `Permissions-Policy`). Por el mapa de `/usuarios` la CSP deja cargar
imágenes de `https://tile.openstreetmap.org` y `Permissions-Policy` permite `geolocation=(self)`; la
geolocalización solo funciona por https.

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
