# Reporte: rutas y backend después de la fase 10 (2026-11-10)

Rama de corrección: `fase/10_fix_rutas_legacy` (sale de `main`, que ya contiene la fase 10).

## Resumen

| # | Síntoma | Causa | Estado |
|---|---|---|---|
| 1 | `/legacy/Usuarios` "dejó de existir" | La migración sacaba al usuario de `/legacy`: `Usuarios.aspx` y el menú redirigían a `/newapp/usuarios` | **Corregido y desplegado** |
| 2 | "El backend no responde" | El backend sí responde. El `Web.config` de MVC en la VM es del 28/oct y no tiene las llaves `backend:*`: `BackendApi` truena antes de llamar al backend (500) | **Corregido** (13/nov): `Web.config` copiado; falta probar con sesión |
| 3 | No se puede entrar por SSH a la VM del IIS | La IP 192.168.50.243 era antes de una VM Linux; `known_hosts` guardaba su llave | **Resuelto**: llave actualizada y llave de esta PC autorizada en la VM |
| 4 | `RutaNewApp` no existe en la VM | El `Web.config` de WebForms en la VM también es anterior a la fase 10 | **Corregido** (13/nov): `Web.config` copiado |

## 1. Ruta `/legacy/Usuarios` (corregido)

**Qué pasaba.** El slice de Usuarios (PR #16) movió la pantalla a `/newapp/usuarios` y dejó
`Usuarios.aspx` como un 302 hacia allá. La Reservación de salas (PR #19) hizo lo contrario: conservó
la URL `/legacy/AngularPage` y el gateway sirve Angular en ella. Quedaron dos criterios distintos y la
ruta legacy de Usuarios desapareció.

**Qué se cambió.** Usuarios ahora usa el mismo mecanismo que salas:

- Gateway: ruta `legacy-usuarios-route` (`/legacy/Usuarios/{**rest}` → `frontend-newapp`) y
  `/legacy/Usuarios.aspx` → 302 a `/legacy/Usuarios`.
- Frontend: montaje `MONTAJE_USUARIOS` (base `/legacy/Usuarios/`) con el Checador de Asistencia dentro
  del marco de WebForms; nginx del contenedor sirve `index.html` también en esa ruta.
- WebForms: `Menu.aspx` vuelve a su enlace original (`Usuarios.aspx`). **No hace falta republicar
  WebForms** para que funcione: el menú publicado hoy apunta a `/newapp/usuarios`, que sigue vivo.
- `/newapp/usuarios` se conserva (misma vista, sin el marco legacy).

**Verificación (sin sesión, por https://192.168.50.10):**

| Ruta | Respuesta |
|---|---|
| `/legacy/Usuarios`, `/legacy/usuarios/` | 200, `index.html` de Angular |
| `/legacy/Usuarios.aspx` | 302 → `/legacy/Usuarios` |
| `/legacy/AngularPage` | 200 (sin cambios) |
| `/legacy/Menu`, `/legacy/Reportes.aspx` | 302 al login de Keycloak (siguen en IIS) |
| `/newapp/usuarios`, `/newapp/menu` | 200 |
| `/api/attendance/check-ins/mine`, `/api/rooms` | 401 (existen, piden token) |

Tests del frontend: 191 en verde. Falta probarlo con sesión en el navegador.

## 2. "El backend no responde" (corregido)

**Lo que sí funciona.** `backend-api` está *healthy* y sin errores en su log. Angular lo usa bien: hoy
hubo `GET /api/rooms` y `/api/rooms/bookings` con 200 desde `/legacy/AngularPage`.

**Lo que falla.** En `/taxes/Home/PaginaAngular`, la llamada
`/taxes/Home/CompararSismos?backend=net10` devuelve **500** (12 veces hoy entre 12:28 y 14:11);
la variante `backend=legacy` responde bien.

**Dato clave.** En los logs de nginx y del gateway no hay **ninguna** petición del IIS a
`/api/taxes/seismic-geojson` ni a `/api/taxes/consolidated-report`. MVC revienta antes de salir hacia
el backend, con una excepción que el controlador no atrapa (atrapa sesión vencida, error del backend,
conexión y timeout; cualquier otra cosa sale como 500).

**Descartado.**
- Configuración: el `Web.config` publicado (`C:\Deploy\brooklynsaas`) tiene todas las llaves
  `backend:*` y `oidc:*` que usa el código.
- Keycloak: `legacy-mvc` ya tiene el mapper `audience-api`.
- Backend: el endpoint existe (401 sin token).

**Causa (confirmada en la VM `EXPERIMENTAL`).** Los binarios y vistas de la VM son idénticos a los
publicados en esta PC (`C:\Deploy`, comparados por hash), pero **los dos `Web.config` no**: los de la
VM son del 28/oct, de antes de la fase 10.

- MVC (`C:\Deploy\brooklynsaas`): faltan `backend:BaseUrl`, `backend:TimeoutSegundos` y
  `backend:PaginaAntiguaNuevoBackend`. `BackendApi` arma su `HttpClient` con
  `AppSettings["backend:BaseUrl"].TrimEnd('/')`: con la llave ausente lanza una excepción que el
  controlador no atrapa → 500, sin salir al backend. `PaginaAntiguaNuevoBackend` cae además a `sql`.
- WebForms (`C:\Deploy\legadosaas`): falta `RutaNewApp`. `Usuarios.aspx` redirigía a `"" + "/usuarios"`
  = `/usuarios`, que no existe (404). Con la corrección 1 el gateway ya no llega a esa página.

**Descartado también:** `Secrets.config` está en la VM; la CA de mkcert está en `LocalMachine\Root`; la
VM alcanza `https://192.168.50.10/api/...` (401 sin token, correcto).

**Arreglo.** Copiar a la VM los `Web.config` publicados el 31/oct (`C:\Deploy\brooklynsaas\Web.config`
y `C:\Deploy\legadosaas\Web.config` de esta PC), respaldando antes los actuales. El resto del contenido
de esos archivos es igual al de la VM (mismas cadenas de conexión y `machineKey`; solo cambia el formato).

**Aplicado el 13/nov.** Respaldo en la VM: `Web.config.antes-fix-20261113-102437` en las dos carpetas
(para revertir, copiarlo sobre `Web.config`). Tras la copia la VM ya tiene `backend:BaseUrl`,
`backend:TimeoutSegundos` y `backend:PaginaAntiguaNuevoBackend=api` en MVC, y `RutaNewApp` en WebForms.
IIS recicló las dos apps y responden: `/taxes/Home/Menu` y `/legacy/Menu` → 302 a Keycloak (sin error de
configuración). Falta probar con sesión la comparativa `net10` en `/taxes/Home/PaginaAngular`.

## 3. SSH a la VM del IIS (resuelto)

La IP 192.168.50.243 primero fue de una VM Linux y luego de la VM Windows: `known_hosts` guardaba la
llave de la Linux. Se reemplazó (respaldo en `known_hosts.old`) y se autorizó la llave de esta PC en
`C:\ProgramData\ssh\administrators_authorized_keys`. En esa VM OpenSSH abre `cmd.exe`: falta el paso de
`DefaultShell` de `Deplegar/despliegueapp.md`.

## Estado de los contenedores

`backend-api`, `frontend-newapp` y `gateway` están en la versión de esta rama (frontend y gateway
reconstruidos hoy). Respaldo de las imágenes anteriores a la fase 10: etiqueta `antes-10`.
