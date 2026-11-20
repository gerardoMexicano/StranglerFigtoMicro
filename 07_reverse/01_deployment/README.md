# 01_deployment — Nginx + Gateway YARP + Keycloak

Un solo `docker-compose.yml` para todos los ambientes. Lo que cambia entre
development / testing / production (dominio, contraseñas, modo de Keycloak)
vive en un env file por ambiente.

```
                 ┌──────────── red edge ─────────────┐
 navegador ──► nginx :443 ─/auth/*──► keycloak ──┐   │
                 │                     │  red identity (interna)
                 └──/*──► gateway      └──► keycloak-db (Postgres)
                         │  red backend
                         ├──► legacy / taxes (Strangler Fig)
                         ├──► backend-api (05_backend) / frontend-newapp (06_frontend)
   keycloak ── plugin ──► legacy-auth ──► SQL Server legacy (usuarios)   [temporal]
   keycloak ── SMTP ────► mailpit (solo development) / SMTP real
```

## Arranque

```bash
# Una sola vez por host: redes compartidas (externas, sin proyecto dueño)
docker network create edge
docker network create backend

cp template.env.deployment .env.development   # llenar contraseñas
docker compose --env-file .env.development up -d --build
```

`edge` y `backend` son externas para que otros stacks (sqlserver, jenkins, etc.)
se unan sin que un `down` las borre. No levantes dos versiones de este stack
a la vez en el mismo host: comparten los puertos 80/443 y el nombre `gateway` en `edge`.

Igual para `.env.testing` y `.env.production`. Los tres están en `.gitignore`;
solo la plantilla se versiona. Si falta una contraseña, compose se niega a arrancar.

| Variable | development | testing / production |
| :--- | :--- | :--- |
| `KC_COMMAND` | `start-dev` | `start` |
| `APP_DOMAIN` | `localhost` | dominio real |
| `APP_SCHEME` | `https` (laboratorio, ver *HTTPS*) | `https` |
| `COMPOSE_PROJECT_NAME` | `lab-development` | `lab-testing` / `lab-production` |

Cambiar de dominio = cambiar `APP_DOMAIN`. `APP_PUBLIC_URL`, `KC_HOSTNAME` y el
issuer OIDC se derivan de él dentro del env file.

## URLs (development)

Con `APP_DOMAIN=192.168.50.10` (la IP de la máquina, para entrar desde otras PCs de la red):

- App / gateway: https://192.168.50.10/ (redirige a la app nueva, https://192.168.50.10/newapp/)
- Consola admin de Keycloak: https://192.168.50.10/auth/admin/ (usuario `KC_BOOTSTRAP_ADMIN_USERNAME`)
- Discovery OIDC: https://192.168.50.10/auth/realms/laboratorio/.well-known/openid-configuration
- Bandeja de Mailpit (correos de verificación / restablecer contraseña): http://localhost:8025
- `http://192.168.50.10/...` responde 301 a la misma ruta en https.

## HTTPS (certificado de mkcert)

nginx termina TLS en el 443 con `nginx/certs/lab.pem` + `lab-key.pem` (carpeta en `.gitignore`; sin
ellos nginx no arranca). Detrás de nginx todo sigue en http por las redes de Docker.

El certificado lo firma la CA local de mkcert de la PC de desarrollo (`mkcert -CAROOT`). **`rootCA-key.pem`
no sale de esa PC ni va a git**: con ella cualquiera emite certificados en los que confían los equipos
que instalaron la CA.

```bash
# En la PC de la CA (una vez): instala la CA en el almacén de esa PC
mkcert -install
# Certificado del laboratorio (SAN: la IP pública, localhost y 127.0.0.1). Vence en ~2 años
mkcert -cert-file nginx/certs/lab.pem -key-file nginx/certs/lab-key.pem 192.168.50.10 localhost 127.0.0.1
```

Cada equipo que entra al laboratorio debe confiar en la CA: copiar **solo** `rootCA.pem` e importarlo
(PowerShell como administrador):

```powershell
Import-Certificate -FilePath .\rootCA.pem -CertStoreLocation Cert:\LocalMachine\Root
```

- PCs de los usuarios: sin la CA el navegador marca el sitio como inseguro y `/newapp` no inicia sesión
  (Web Crypto solo existe en contexto seguro).
- IIS (192.168.50.243): obligatorio. Las apps legacy leen el discovery de Keycloak y WebForms/MVC llaman a
  `/api` por https desde el servidor.
- Firefox usa su propio almacén: activar `security.enterprise_roots.enabled` o importar la CA ahí.
- Renovar el certificado: volver a correr `mkcert` y `docker compose ... up -d --no-deps --force-recreate nginx`.

El puerto 80 depende de `APP_SCHEME` (`nginx/templates/puerto80-<esquema>.inc.template`): con `https`
redirige con 301 a `APP_PUBLIC_URL`; con `http` sirve el sitio igual que el 443.

nginx resuelve `keycloak` y `gateway` con el DNS de Docker en cada petición (`resolver` en
`sitio.inc.template`): recrear esos contenedores no requiere reiniciar nginx.

Para probar desde Windows usar el navegador o PowerShell; `curl.exe` (Schannel) no valida bien el SAN de
IP del certificado. Desde Linux: `curl --cacert rootCA.pem https://192.168.50.10/...`.

### Corte de http a https

Así se hizo en el laboratorio; sirve igual para otro ambiente. Desde `07_reverse/01_deployment`:

1. Antes del corte, que Keycloak acepte las dos versiones de cada URI (no cambia nada para los usuarios):
   `./keycloak/scripts/realm-https.sh development agregar`
2. Respaldo del env (`cp .env.development .env.development.http.bak`) y `APP_SCHEME=https`.
3. Recrear lo que toma el issuer del env:
   - `docker compose --env-file .env.development up -d --no-deps keycloak gateway nginx`
   - `05_backend` y `06_frontend` con su propio compose (`up -d`, **sin** `--remove-orphans`).
4. En el IIS: respaldar `C:\Deploy\taxes` y `C:\Deploy\legacy`, publicar las apps con `Release`
   (`Web.Release.config` ya trae https) y reciclar los pools `legadosaas-taxes-Pool` y `legadosaas-legacy-Pool`.
5. Verificar desde otra PC con la CA: login en `/taxes`, SSO a `/legacy`, `LEgFrontNEWBACK.aspx`, login
   en `/newapp` y cerrar sesión.
6. Días después, sin problemas: `./keycloak/scripts/realm-https.sh development quitar-http` (quita las
   URIs http y pasa `rootUrl` / `baseUrl` a https) y borrar `.env.development.http.bak`.

Regresar a http (antes del paso 6): restaurar `.env.development.http.bak`, repetir el paso 3 y, en el IIS,
restaurar los respaldos y reciclar los pools. El realm aún tiene las URIs http, así que Keycloak no se toca.
Después del paso 6, regresar exige volver a agregar las URIs http en la consola de Keycloak.

## Realm importado

`keycloak/import/laboratorio-realm.json` se importa en el primer arranque
(estrategia `IGNORE_EXISTING`: si el realm ya existe en la BD no se toca).
Los valores `${IMPORT_*}` salen del env file.

- `frontend-spa`: cliente público, Authorization Code + PKCE (S256), redirect `${APP_PUBLIC_URL}/*`.
- `backend-api`: resource server; el frontend emite access tokens con `aud: backend-api`.
- `legacy-cutover`: cuenta de servicio del comando `corte` (permisos mínimos sobre usuarios).
- `legacy-mvc` / `legacy-webforms`: apps legacy (OWIN), confidenciales, Authorization Code + PKCE (S256).
  Solo vuelven a `<url>/signin-oidc`, con dos URLs por app: la del gateway (`LEGACY_*_URL`: MVC en `/taxes`,
  WebForms en `/legacy`) y la de IIS Express (`LEGACY_*_DEV_URL`). El SSO entre ellas y con `frontend-spa`
  lo da la sesión de Keycloak.
  Tanto `legacy-webforms` como `legacy-mvc` emiten access tokens con `aud: backend-api` (mapper `audience-api`)
  para llamar a `05_backend` por `/api` con el token del usuario.
- Roles de realm: `usuario`, `admin`, `invitado` (los mismos valores que `usuarios.rol` del legacy).
- `verifyEmail: true` y SMTP tomado de las variables `SMTP_*`.
- Proveedor de migración `legacy-migration` (ver abajo).

Keycloak se construye con `keycloak/Dockerfile`: imagen oficial + plugin
[keycloak-user-migration](https://github.com/daniel-frak/keycloak-user-migration) 6.2.4
(verificado por SHA-256).

Para reimportar después de editar el JSON en development:
`docker compose --env-file .env.development down -v` (borra la BD de Keycloak).

En un realm que ya existe, el mapper `audience-api` de `legacy-webforms` y `legacy-mvc` se agrega sin reimportar:
`./keycloak/scripts/audiencia-legacy-webforms.sh <ambiente>` y `./keycloak/scripts/audiencia-legacy-mvc.sh <ambiente>`
(usan el admin `KC_BOOTSTRAP_ADMIN_*`; si el mapper ya está, no hacen nada).

Para desarrollar `06_frontend` con `ng serve`, `frontend-spa` debe aceptar `http://localhost:4200`
(redirect, web origin y post logout). No va en el JSON: se agrega y se quita a mano, solo en development
(el script se niega con otro ambiente o si el env file no tiene `APP_ENV=development` y
`KC_COMMAND=start-dev`):

```bash
./keycloak/scripts/frontend-spa-localhost.sh development ver       # muestra las URIs del cliente
./keycloak/scripts/frontend-spa-localhost.sh development agregar
./keycloak/scripts/frontend-spa-localhost.sh development quitar    # al terminar
```

## Validar tokens desde una API .NET

Los servicios en la red `backend` reciben (ver `x-oidc-env` en el compose):

- `Oidc__Authority` — issuer público, p. ej. `https://192.168.50.10/auth/realms/laboratorio`
- `Oidc__MetadataAddress` — discovery por red interna (`http://keycloak:8080/auth/...`)
- `Oidc__Audience` — `backend-api`

El discovery interno devuelve el issuer público pero `jwks_uri` interno
(`KC_HOSTNAME_BACKCHANNEL_DYNAMIC=true`), así que la API valida tokens sin salir a internet.

## Apps legacy (MVC / WebForms) con Keycloak

`01_webapp_mvc` y `02_webapp_webforms` (.NET Framework 4.8.1) inician sesión con Keycloak vía
OWIN (Katana 4.2.2 + IdentityModel 5.7.0): Authorization Code + PKCE, sin formulario de contraseña.

```
sin sesión ─► Account/Login (MVC) o Login.aspx (WebForms) ─► Keycloak ─► <app>/signin-oidc
           ─► cookie de la app (.AUTH.MVC / .AUTH.WEBFORMS) ─► [Authorize] / Request.IsAuthenticated
logout     ─► borra la cookie de la app + end_session de Keycloak (id_token_hint) ─► post_logout_redirect_uri
```

- Pipeline en `Startup.cs` / `Startup.vb`; `Web.config` con `<authentication mode="None" />`.

- `SaveTokens=true`: el ticket con claims + `access_token`, `refresh_token` e `id_token` se guarda en la
  caché de IIS (`App_Start/SesionEnServidor`, por `auth:SesionMinutos` deslizante) y la cookie solo lleva
  su llave cifrada. Así las cookies de las dos apps nunca rebasan el límite de encabezados de petición de
  nginx (8 KB por línea: 400 "Request Header Or Cookie Too Large"). Si IIS recicla el app pool la sesión
  se pierde y el usuario vuelve a entrar por Keycloak sin contraseña.
- SSO: cada app tiene su cookie; entrar en una deja abierta la sesión de Keycloak y la otra entra sin
  pedir contraseña. Cerrar sesión en cualquiera cierra la de Keycloak y la de las otras apps (ver
  *Logout global*).

### Logout global (front-channel)

```
"Cerrar sesión" en /taxes, /legacy o /newapp ─► Keycloak end_session (cierra la sesión SSO)
   └─► página de Keycloak con un iframe por app con sesión:
         /taxes/Account/FrontchannelLogout?iss=…&sid=…    MVC borra .AUTH.MVC
         /legacy/FrontchannelLogout.ashx?iss=…&sid=…      WebForms borra .AUTH.WEBFORMS
   └─► post_logout_redirect_uri de la app que pidió salir
/newapp (pestaña abierta) ─► check session de Keycloak ─► "changed" ─► borra sus tokens y vuelve a /newapp/
```

- Cada endpoint borra su cookie solo si `iss` es su `oidc:Authority` y `sid` es el de la sesión
  (`App_Start/CierreDesdeKeycloak`): otra página no puede cerrar la sesión con un iframe sin conocer el
  `sid`. Al borrar la cookie se borra también la sesión de `SesionEnServidor`. Responde 200 vacío, sin caché.
- En WebForms el handler es anónimo (`<location path="FrontchannelLogout.ashx">` en el `Web.config`).
- Keycloak: `frontchannelLogout`, `frontchannel.logout.url` y `frontchannel.logout.session.required`
  en `legacy-mvc` y `legacy-webforms`. En un realm nuevo los trae el JSON; en uno existente:

  ```bash
  ./keycloak/scripts/logout-global.sh development ver       # ver | aplicar | quitar
  ```

  Orden: primero publicar las apps (los endpoints deben responder 200), después `aplicar`.
  `quitar` deja la URL vacía y todo vuelve a como era (cada app cierra solo su sesión).
- Límites:
  - Solo cubre el logout que pasa por el navegador. Cerrar la sesión desde la consola de Keycloak o
    que venza su sesión no avisa a las apps; sus cookies siguen hasta `auth:SesionMinutos`
    (back-channel lo cubriría: Keycloak llamaría al IIS de servidor a servidor).
  - Las sesiones de IIS Express no se cierran: la URL apunta a las apps publicadas.
  - Mientras Keycloak carga los iframes se ve un momento una página en blanco.

### Desplegadas detrás del gateway

```
navegador ─► nginx ─► gateway ─► IIS 192.168.50.243:8081/taxes  (MVC)
                                 IIS 192.168.50.243:8081/legacy (WebForms)
                                 backend-api:8080/api           (05_backend, red backend)
```

- `/api/*` va al backend .NET 10 (`BACKEND_API_BASE_URL`, por defecto `http://backend-api:8080`) sin
  quitar el prefijo. El backend se levanta con su propio compose (`05_backend`) en la red `backend`;
  si no está arriba, el gateway responde 502 en `/api/*` y el resto sigue funcionando.

- Las apps son aplicaciones virtuales de IIS en `/taxes` y `/legacy` (`Deplegar/launch-sites.ps1`) y el
  gateway no quita el prefijo: `~/`, `Url.Action` y `/signin-oidc` salen bien sin reescribir URLs.
- `Web.Release.config` cambia `oidc:RedirectUri`, `oidc:PostLogoutRedirectUri`, los enlaces entre apps
  (`RutaAppMVC` / `RutaAppWebF`) y `proxy:Confiables` a los valores del laboratorio.
- `proxy:Confiables`: IPs desde las que la app acepta `X-Forwarded-Proto` / `X-Forwarded-Host`
  (`App_Start/OwinProxy`). Sin esto Katana arma la URL de regreso del login con el host interno de IIS
  y decide `Secure` en las cookies como si todo fuera http. El gateway (`UseForwardedHeaders`) propaga
  el esquema y el host que vio nginx.
- La cookie `OpenIdConnect.nonce` sale con `SameSite=Lax` (`SameSiteLaxCookieManager`): con `None` el
  navegador la descarta fuera de https y el login falla al volver de Keycloak.
- En la consola de Keycloak, `legacy-mvc` y `legacy-webforms` deben aceptar las URLs públicas
  (`${APP_PUBLIC_URL}/taxes/signin-oidc`, `${APP_PUBLIC_URL}/taxes/`, igual con `/legacy`).
- `Secrets.config` se copia a mano (o desde Jenkins) a la carpeta de cada app en el servidor.

- https: `Web.Release.config` y el `Web.config` base usan `https://192.168.50.10/...` y
  `oidc:RequireHttpsMetadata=true`. El IIS (y la PC donde se depura con IIS Express) debe tener
  instalada la CA de mkcert (ver *HTTPS*).

### WebForms contra el backend nuevo (`LEgFrontNEWBACK.aspx`)

```
navegador ─► nginx ─► gateway ─► IIS /legacy/LEgFrontNEWBACK.aspx
                                   │  code-behind, Bearer <token del usuario>
                                   ▼
             nginx ─► gateway ─► backend-api:8080/api/sales/*  ─► WideWorldImporters
```

- La página llama al backend desde el servidor (`Backend/BackendApi.vb`): el token no llega al
  navegador. El access token sale de la sesión en IIS (`SaveTokens` + `SesionEnServidor`); si vence
  se renueva con el refresh token (`Backend/TokenDeAcceso.vb`). Si Keycloak ya no lo renueva, la página
  cierra la sesión local y se recarga para volver a pasar por Keycloak.
- `Web.config`:
  - `backend:BaseUrl` — `https://192.168.50.10/api/` (por nginx + gateway, igual que el login).
  - `backend:TimeoutSegundos` — `300`.
  - `backend:LEgFrontNEWBACK` — `api` (backend nuevo) o `sql` (consulta directa como antes, para
    regresar sin desplegar).
- Requisitos: el mapper `audience-api` en `legacy-webforms` (ver *Realm importado*) y el contenedor de
  `05_backend` arriba en la red `backend`, con `WWI_DB_CONNECTION` en el `.env.<ambiente>`.
- Con `api` el grid pagina en el servidor: cada página es una llamada de 50 filas. El backend ordena por
  fecha descendente; la consulta directa no tenía orden, así que las filas pueden salir en otro orden
  pero el total es el mismo.

Verificación:

```bash
# Sin token: 401 del backend (WWW-Authenticate: Bearer). 404 = el gateway no tiene la ruta /api
curl -i --cacert rootCA.pem https://192.168.50.10/api/sales/orders
```

En el navegador, `https://192.168.50.10/legacy/LEgFrontNEWBACK.aspx`: arriba dice el origen de datos
activo; *Cargar Registros* llena las 5 tablas. Con el rango por defecto (2013-01-01 a 2016-12-31) el
paginador debe dar los mismos totales con `api` y con `sql`.

### MVC contra el backend nuevo (`Home/PaginaAntiguaNuevoBackend`)

```
navegador ─► nginx ─► gateway ─► IIS /taxes/Home/PaginaAntiguaNuevoBackend
                                   │  Controller asíncrono, Bearer <token del usuario>
                                   ▼
             nginx ─► gateway ─► backend-api:8080/api/taxes/consolidated-report ─► taxes_proto
```

- El controlador llama al backend desde el servidor (`Backend/BackendApi.cs`): el token no llega al
  navegador. El access token sale de `SesionEnServidor` y se renueva con el refresh token
  (`Backend/TokenDeAcceso.cs`).
- `Web.config`:
  - `backend:BaseUrl` — `https://192.168.50.10/api/`
  - `backend:TimeoutSegundos` — `300`
  - `backend:PaginaAntiguaNuevoBackend` — `api` (backend nuevo paginado) o `sql` (consulta directa
    bloqueante original para regresar sin desplegar).
- Requisitos: el mapper `audience-api` en `legacy-mvc` (`./keycloak/scripts/audiencia-legacy-mvc.sh <ambiente>`)
  y `TAXES_DB_CONNECTION` en el `.env.<ambiente>` consumido por `05_backend`.

Verificación:

```bash
# Sin token: 401 del backend (WWW-Authenticate: Bearer)
curl -i --cacert rootCA.pem https://192.168.50.10/api/taxes/consolidated-report
```

## App nueva (`/newapp`, `06_frontend`)

```
navegador ─► nginx ─► gateway ─► /           302 → /newapp/
                                 /newapp/*   frontend-newapp:8080 (06_frontend, red backend)
                                 /legacy/AngularPage*      frontend-newapp:8080 (Reservación de salas)
                                 /legacy/AngularPage.aspx  302 → /legacy/AngularPage
                                 /legacy/Usuarios*         frontend-newapp:8080 (Checador de Asistencia)
                                 /legacy/Usuarios.aspx     302 → /legacy/Usuarios
                                 /legacy/* (lo demás)      IIS, como siempre
             Angular ─► nginx ─► /auth/*     Keycloak (cliente frontend-spa, PKCE)
                      ─► nginx ─► gateway ─► /api/*  backend-api (Bearer del usuario)
```

- Ruta `frontend-newapp-route` (`/newapp/{**catch-all}`) sin quitar el prefijo: la app se construye con
  `baseHref: /newapp/`. Destino en `FRONTEND_NEWAPP_BASE_URL` (por defecto `http://frontend-newapp:8080`).
- `/` responde 302 a `/newapp/` (`Gateway/Program.cs`). Es 302 y no 301 para que el navegador no guarde
  la redirección y la entrada se pueda mover. `/taxes`, `/legacy` y `/api` no cambian.
- Ruta `legacy-angularpage-route` (`/legacy/AngularPage/{**rest}`, `Order: -1`) al mismo cluster
  `frontend-newapp-cluster`: gana a `legacy-route`, así la página sigue bajo `/legacy` pero la sirve Angular
  (Reservación de salas, datos en `/api/rooms`). `Program.cs` manda `AngularPage.aspx` a `/legacy/AngularPage`
  con 302, así el enlace de `Menu.aspx` funciona sin tocar WebForms.
  - **Rollback:** quitar esa ruta y el `MapMethods` de `AngularPage.aspx`, y redesplegar el gateway: `/legacy/AngularPage`
    vuelve a IIS, donde `AngularPage.aspx` sigue publicada.
  - Requiere `sqlserver/db/06_salas.sql` en `Laboratory` y `BACKEND_DB_CONNECTION` con `INSERT` y `DELETE` (p. ej. `app_escritor`).
- La app es una sola imagen; su `config.json` (Keycloak, API, URLs de las apps legacy) se genera al
  arrancar el contenedor. Detalle en [`06_frontend/README.md`](../../06_frontend/README.md).
- El contenedor se levanta con su propio compose (`06_frontend`) y comparte el proyecto
  `COMPOSE_PROJECT_NAME` con este stack y con `05_backend`. **Nunca usar `--remove-orphans`** en ninguno
  de los tres: cada uno ve a los servicios de los otros como huérfanos y los borraría.
- Orden al desplegar: primero `06_frontend`, después el gateway. Si `frontend-newapp` no está arriba,
  `/` y `/newapp/*` dan 502 y lo demás sigue funcionando.

Verificación:

```bash
curl -i --cacert rootCA.pem https://192.168.50.10/              # 302, Location: /newapp/
curl -i --cacert rootCA.pem https://192.168.50.10/newapp/menu   # 200 (lo sirve frontend-newapp)
curl -i --cacert rootCA.pem https://192.168.50.10/legacy/AngularPage       # 200, index.html de Angular (<base href="/newapp/">)
curl -i --cacert rootCA.pem https://192.168.50.10/legacy/AngularPage.aspx  # 302, Location: /legacy/AngularPage
curl -i --cacert rootCA.pem https://192.168.50.10/legacy/Usuarios          # 200, index.html de Angular (legacy-usuarios-route)
curl -i --cacert rootCA.pem https://192.168.50.10/legacy/Usuarios.aspx     # 302, Location: /legacy/Usuarios
```

En el navegador: `/` → presentación → *Entrar* → Keycloak → menú. Desde el menú, `/taxes` y `/legacy`
entran sin pedir contraseña (sesión SSO de Keycloak).

### Roles

Roles de realm (los mismos que el plugin de migración trae del legacy):

| Opción | `usuario` | `admin` | `invitado` |
| :--- | :---: | :---: | :---: |
| BrokenTaxes (`/taxes`) | ✓ | ✓ | — |
| Legacy WebForms (`/legacy`) | ✓ | ✓ | — |
| LEgFrontNEWBACK (`/newapp` y `/api/sales`) | ✓ | ✓ | ✓ |
| PaginaAntiguaNuevoBackend (`/api/taxes/consolidated-report`) | ✓ | ✓ | ✓ |
| Usuarios: ver ubicación e historial (`/newapp/usuarios`, `GET /api/attendance/check-ins/mine`) | ✓ | ✓ | ✓ |
| Usuarios: registrar asistencia (`POST /api/attendance/check-ins`) | ✓ | ✓ | — |
| Reservación de salas: ver la agenda (`/legacy/AngularPage`, `GET /api/rooms*`) | ✓ | ✓ | ✓ |
| Reservación de salas: reservar y cancelar (`POST`/`DELETE /api/rooms/bookings`; `admin` cancela cualquiera) | ✓ | ✓ | — |


- `/api/sales` y `/api/taxes` exigen la política `Lectura` (cualquiera de los tres): sin rol, 403. Es la que protege los
  datos, también cuando llaman WebForms o MVC (su token trae `realm_access` por el scope `roles`).
- En `/newapp`, el menú y las rutas siguen la tabla; sin ninguno de los tres roles se ve `/sin-acceso`.
- Ocultar `/taxes` y `/legacy` en el menú no los protege: los permisos dentro de las apps legacy siguen
  siendo los suyos.

Pendientes:

- Quitar del realm las URLs de IIS Express (`LEGACY_*_DEV_URL`) cuando ya no se use.
- Despliegue de `06_frontend` desde Jenkins.

## Rutas legacy retiradas (`/rutas-retiradas`)

El gateway ya no manda a IIS lo que empiece con `/legacy` (WebForms) ni `/taxes` (MVC, "Second System
Broken"): responde 302 a `/rutas-retiradas?desde=<ruta>`, una página del propio gateway (410 Gone)
con "Estas rutas ya no existen. Sentimos las molestias." y un botón **Volver al inicio** (`/` → `/newapp/`).
Funciona para cualquier método, también POST.

- Configuración: sección `RutasRetiradas` de `Gateway/appsettings.json`.
  - `Prefijos`: se comparan por segmento (`/legacy` cubre `/legacy/...`, no `/legacyx`).
  - `Excepciones`: páginas ya migradas a Angular que conservan su URL bajo `/legacy`
    (`/legacy/AngularPage`, `/legacy/Usuarios`). Se comparan por prefijo, así que también cubren su `.aspx`.
- Código: `Gateway/RutasRetiradas.cs`. Pruebas: `Gateway.Tests` (`dotnet test` en esa carpeta).
- No afecta el login: Keycloak valida contra `legacy-auth` directo por la red de Docker, no por el gateway.
  El logout front-channel de los clientes legacy cae en la página retirada dentro de su iframe oculto, sin efecto visible.
- **Rollback:** `RutasRetiradas__Habilitado=false` en el servicio `gateway` del compose (o `"Habilitado": false`
  en `appsettings.json`) y recrear el gateway: `/legacy` y `/taxes` vuelven al proxy hacia IIS como antes.

## Migración de usuarios al primer login

```
1er login de "juan" (no existe en Keycloak)
  Keycloak ──GET  /user-migration-support/juan──► legacy-auth ──► usuarios (username, rol, activo)
  Keycloak ──POST /user-migration-support/juan──► legacy-auth ──► valida SHA-512(salt + password)
  válida → Keycloak guarda la contraseña con SU hash (argon2) y corta la liga con el legacy
         → pide email/nombre (perfil obligatorio) → manda correo de verificación → token
2º login → solo Keycloak; el legacy ya no participa
```

- `LegacyAuth/` (.NET 10) implementa los dos endpoints. Solo está en la red `backend`,
  sin puertos publicados, y exige `Authorization: Bearer ${LEGACY_AUTH_API_TOKEN}`.
- Lee la BD legacy con `app_lector` (`LEGACY_DB_CONNECTION`). Requiere haber corrido
  `sqlserver/db/03_usuarios_hash_expand.sql` en esa instancia.
- El rol del legacy (`usuarios.rol`) se asigna como rol de realm.
- Usuarios con `activo = 0` no pueden migrar.

Avance de la migración: en la consola, *Users* muestra a los migrados; los que siguen
ligados al legacy tienen *Federation link* = `legacy-migration`.

## Corte (fin de la ventana, p. ej. mes 6)

1. Consola de Keycloak → *User federation* → `legacy-migration` → **Enabled: off** → *Save*.
2. Simulación (no cambia nada, muestra el plan por usuario):
   ```bash
   docker compose --env-file .env.<ambiente> run --rm legacy-auth corte
   ```
3. Aplicar:
   ```bash
   docker compose --env-file .env.<ambiente> run --rm legacy-auth corte --aplicar
   ```

Resultado: los migrados no se tocan; los que nunca completaron el login quedan en Keycloak
**deshabilitados y con su rol**. Se reactivan desde la mesa de ayuda (el admin captura su
email y envía *Reset password* desde la consola). Es idempotente: se puede volver a correr.

Después del corte se puede quitar el servicio `legacy-auth`, el cliente `legacy-cutover`
y el proveedor `legacy-migration`.
