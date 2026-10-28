# 01_deployment — Nginx + Gateway YARP + Keycloak

Un solo `docker-compose.yml` para todos los ambientes. Lo que cambia entre
development / testing / production (dominio, contraseñas, modo de Keycloak)
vive en un env file por ambiente.

```
                 ┌──────────── red edge ─────────────┐
 navegador ──► nginx :80 ──/auth/*──► keycloak ──┐   │
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
a la vez en el mismo host: comparten el puerto 80 y el nombre `gateway` en `edge`.

Igual para `.env.testing` y `.env.production`. Los tres están en `.gitignore`;
solo la plantilla se versiona. Si falta una contraseña, compose se niega a arrancar.

| Variable | development | testing / production |
| :--- | :--- | :--- |
| `KC_COMMAND` | `start-dev` | `start` |
| `APP_DOMAIN` | `localhost` | dominio real |
| `APP_SCHEME` | `http` | `https` (requiere TLS delante de Nginx) |
| `COMPOSE_PROJECT_NAME` | `lab-development` | `lab-testing` / `lab-production` |

Cambiar de dominio = cambiar `APP_DOMAIN`. `APP_PUBLIC_URL`, `KC_HOSTNAME` y el
issuer OIDC se derivan de él dentro del env file.

## URLs (development)

Con `APP_DOMAIN=192.168.50.10` (la IP de la máquina, para entrar desde otras PCs de la red):

- App / gateway: http://192.168.50.10/ (redirige a la app nueva, http://192.168.50.10/newapp/)
- Consola admin de Keycloak: http://192.168.50.10/auth/admin/ (usuario `KC_BOOTSTRAP_ADMIN_USERNAME`)
- Discovery OIDC: http://192.168.50.10/auth/realms/laboratorio/.well-known/openid-configuration
- Bandeja de Mailpit (correos de verificación / restablecer contraseña): http://localhost:8025

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
  `legacy-webforms` también emite access tokens con `aud: backend-api` (mapper `audience-api`): WebForms
  llama a `05_backend` por `/api` con el token del usuario.
- Roles de realm: `usuario`, `admin`, `invitado` (los mismos valores que `usuarios.rol` del legacy).
- `verifyEmail: true` y SMTP tomado de las variables `SMTP_*`.
- Proveedor de migración `legacy-migration` (ver abajo).

Keycloak se construye con `keycloak/Dockerfile`: imagen oficial + plugin
[keycloak-user-migration](https://github.com/daniel-frak/keycloak-user-migration) 6.2.4
(verificado por SHA-256).

Para reimportar después de editar el JSON en development:
`docker compose --env-file .env.development down -v` (borra la BD de Keycloak).

En un realm que ya existe, el mapper `audience-api` de `legacy-webforms` se agrega sin reimportar:
`./keycloak/scripts/audiencia-legacy-webforms.sh <ambiente>` (usa el admin `KC_BOOTSTRAP_ADMIN_*`;
si el mapper ya está, no hace nada).

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

- `Oidc__Authority` — issuer público, p. ej. `http://localhost/auth/realms/laboratorio`
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
- Configuración en `appSettings` del `Web.config`: `oidc:Authority`, `oidc:ClientId`, `oidc:RedirectUri`,
  `oidc:PostLogoutRedirectUri`, `oidc:RequireHttpsMetadata` y `auth:SesionMinutos` (sesión local deslizante).
- El secreto del cliente va en `Secrets.config` junto al `Web.config` (ignorado por git; plantilla
  `Secrets.config.example`). Es el mismo valor que `LEGACY_MVC_CLIENT_SECRET` / `LEGACY_WEBFORMS_CLIENT_SECRET`.
- `User.Identity.Name` es el username del legacy (claim `preferred_username`).
- La sesión (claims + access, refresh e id token de Keycloak) vive en la memoria de IIS
  (`App_Start/SesionEnServidor`); la cookie solo lleva su llave cifrada. Con los tokens dentro ocupaba
  varios KB por app y, con las de MVC y WebForms juntas, el encabezado Cookie puede rebasar el límite de
  nginx (8 KB por línea: 400 "Request Header Or Cookie Too Large"). Si IIS recicla el app pool la sesión
  se pierde y el usuario vuelve a entrar por Keycloak sin contraseña.
- SSO: cada app tiene su cookie; entrar en una deja abierta la sesión de Keycloak y la otra entra sin
  pedir contraseña. Cerrar sesión en cualquiera cierra la de Keycloak.

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

Al pasar a https (TLS en nginx, `APP_SCHEME=https`): cambiar las URLs `http://` del `Web.Release.config`
y de Keycloak a `https://`, y `oidc:RequireHttpsMetadata` a `true`.

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
  - `backend:BaseUrl` — `http://192.168.50.10/api/` (por nginx + gateway, igual que el login).
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
curl -i http://192.168.50.10/api/sales/orders
```

En el navegador, `http://192.168.50.10/legacy/LEgFrontNEWBACK.aspx`: arriba dice el origen de datos
activo; *Cargar Registros* llena las 5 tablas. Con el rango por defecto (2013-01-01 a 2016-12-31) el
paginador debe dar los mismos totales con `api` y con `sql`.

## App nueva (`/newapp`, `06_frontend`)

```
navegador ─► nginx ─► gateway ─► /           302 → /newapp/
                                 /newapp/*   frontend-newapp:8080 (06_frontend, red backend)
             Angular ─► nginx ─► /auth/*     Keycloak (cliente frontend-spa, PKCE)
                      ─► nginx ─► gateway ─► /api/*  backend-api (Bearer del usuario)
```

- Ruta `frontend-newapp-route` (`/newapp/{**catch-all}`) sin quitar el prefijo: la app se construye con
  `baseHref: /newapp/`. Destino en `FRONTEND_NEWAPP_BASE_URL` (por defecto `http://frontend-newapp:8080`).
- `/` responde 302 a `/newapp/` (`Gateway/Program.cs`). Es 302 y no 301 para que el navegador no guarde
  la redirección y la entrada se pueda mover. `/taxes`, `/legacy` y `/api` no cambian.
- La app es una sola imagen; su `config.json` (Keycloak, API, URLs de las apps legacy) se genera al
  arrancar el contenedor. Detalle en [`06_frontend/README.md`](../../06_frontend/README.md).
- El contenedor se levanta con su propio compose (`06_frontend`) y comparte el proyecto
  `COMPOSE_PROJECT_NAME` con este stack y con `05_backend`. **Nunca usar `--remove-orphans`** en ninguno
  de los tres: cada uno ve a los servicios de los otros como huérfanos y los borraría.
- Orden al desplegar: primero `06_frontend`, después el gateway. Si `frontend-newapp` no está arriba,
  `/` y `/newapp/*` dan 502 y lo demás sigue funcionando.

Verificación:

```bash
curl -i http://192.168.50.10/              # 302, Location: /newapp/
curl -i http://192.168.50.10/newapp/menu   # 200 (lo sirve frontend-newapp)
```

En el navegador: `/` → presentación → *Entrar* → Keycloak → menú. Desde el menú, `/taxes` y `/legacy`
entran sin pedir contraseña (sesión SSO de Keycloak).

Pendientes:

- *Cerrar sesión* en `/newapp` cierra la sesión de Keycloak, pero las sesiones locales de `/taxes` y
  `/legacy` siguen vivas hasta que expiren.
- Menú y rutas por rol (`usuario`, `admin`, `invitado`).
- Quitar del realm las URLs de IIS Express (`LEGACY_*_DEV_URL`) cuando ya no se use.
- Despliegue de `06_frontend` desde Jenkins.

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
