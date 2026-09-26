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
                         └──► legacy / taxes (Strangler Fig)
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

- App / gateway: http://192.168.50.10/
- Consola admin de Keycloak: http://192.168.50.10/auth/admin/ (usuario `KC_BOOTSTRAP_ADMIN_USERNAME`)
- Discovery OIDC: http://192.168.50.10/auth/realms/laboratorio/.well-known/openid-configuration

## Realm importado

`keycloak/import/laboratorio-realm.json` se importa en el primer arranque
(estrategia `IGNORE_EXISTING`: si el realm ya existe en la BD no se toca).
Los valores `${IMPORT_*}` salen del env file.

- `frontend-spa`: cliente público, Authorization Code + PKCE (S256), redirect `${APP_PUBLIC_URL}/*`.
- `backend-api`: resource server; el frontend emite access tokens con `aud: backend-api`.
- Roles de realm: `usuario`, `admin`.

Para reimportar después de editar el JSON en development:
`docker compose --env-file .env.development down -v` (borra la BD de Keycloak).

## Validar tokens desde una API .NET

Los servicios en la red `backend` reciben (ver `x-oidc-env` en el compose):

- `Oidc__Authority` — issuer público, p. ej. `http://localhost/auth/realms/laboratorio`
- `Oidc__MetadataAddress` — discovery por red interna (`http://keycloak:8080/auth/...`)
- `Oidc__Audience` — `backend-api`

El discovery interno devuelve el issuer público pero `jwks_uri` interno
(`KC_HOSTNAME_BACKCHANNEL_DYNAMIC=true`), así que la API valida tokens sin salir a internet.
