# `05_backend` — Backend .NET 10 en Clean Architecture (Docker / Sin IIS)

Microservicio backend construido en **.NET 10 (`net10.0`)** bajo **Clean Architecture** como parte de la modernización progresiva (*Strangler Fig*).

- **Cero acoplamiento a IIS:** Se ejecuta 100% sobre **Kestrel** en un contenedor Linux (`mcr.microsoft.com/dotnet/aspnet:10.0`).
- **Inmutabilidad entre ambientes:** El código y el `Dockerfile` no cambian entre `development`, `testing` o `production`. Toda la configuración proviene del archivo `.env.<ambiente>` ubicado en `../07_reverse/01_deployment/`.
- **Identidad delegada en Keycloak:** Actúa como *Resource Server* OAuth 2.0 / OIDC validando tokens JWT emitidos por Keycloak a través de la red interna Docker `backend`.
- **Acceso a datos con Dapper:** Consultas rápidas y explícitas mediante `IDbConnectionFactory` (`DefaultConnection`) e `IWwiDbConnectionFactory` (`WideWorldImporters`).
- **CQRS con Abstracciones Propias:** Sin dependencias de terceros de pago (`ICommandHandler`, `IQueryHandler` + `Result<T>`).

---

## 1. Estructura de Clean Architecture

```text
05_backend/
├── src/
│   ├── Domain/          # Núcleo empresarial sin dependencias externas (Result<T>, Error, Entities)
│   ├── Application/     # Casos de uso CQRS propios (Users, Sales/LEgFrontNEWBACK, PagedResult<T>)
│   ├── Infrastructure/  # Adaptadores: Dapper (SqlConnectionFactory, WwiSqlConnectionFactory, SalesReadRepository), Keycloak JWT/Roles, Token Exchange RFC 8693
│   └── Api/             # Minimal APIs (SystemEndpoints, SalesEndpoints) + OpenAPI nativo + ProblemDetails RFC 7807 + HealthChecks
├── tests/
│   ├── UnitTests/       # Pruebas unitarias rápidas de Domain, Application e Infrastructure
│   └── IntegrationTests/# Pruebas de integración HTTP con WebApplicationFactory<Program>
├── Dockerfile           # Build multi-stage para Linux Kestrel (puerto 8080, usuario no-root)
├── docker-compose.yml   # Orquestación independiente conectada a la red externa "backend"
└── Migracion.slnx       # Solución .NET 10
```

---

## 2. Metodología de Desarrollo: TDD con Intervención

Toda nueva funcionalidad en este repositorio debe seguir estrictamente el flujo **TDD (Test-Driven Development)**:

1. **Fase 1 — Red + Intervención:** Se escriben primero los contratos mínimos y las pruebas en `tests/UnitTests` y `tests/IntegrationTests`. Se revisan/aprueban los escenarios de prueba antes de escribir la lógica de negocio definitiva.
2. **Fase 2 — Green:** Se implementa el código definitivo en `Domain`, `Application`, `Infrastructure` y `Api` hasta que `dotnet test` pase al 100%.
3. **Fase 3 — Refactor:** Se optimiza el código manteniendo todas las pruebas en verde.

Para ejecutar las pruebas localmente:

```bash
dotnet test Migracion.slnx
```

---

## 3. Variables de Entorno (Archivo `.env.<ambiente>` compartido)

Este contenedor **no** almacena secretos ni URLs en `appsettings.json`. En su lugar, lee el archivo de variables de entorno del stack de despliegue en `../07_reverse/01_deployment/.env.<ambiente>` (basado en [`07_reverse/01_deployment/template.env.deployment`](../07_reverse/01_deployment/template.env.deployment)).

### Catálogo de Variables Consumidas por `05_backend/docker-compose.yml`

| Variable en `07_reverse/01_deployment/.env.<ambiente>` | Variable inyectada al contenedor `.NET 10` | Obligatoria | Descripción |
| :--- | :--- | :---: | :--- |
| `COMPOSE_PROJECT_NAME` | *(Nombre del proyecto Compose)* | Sí | Prefija el stack como `${COMPOSE_PROJECT_NAME}-backend` (ej. `lab-development-backend`) para no colisionar con el compose de `07_reverse`. |
| `APP_ENV` | *(Tag de imagen Docker)* | Sí | Etiqueta la imagen construida como `backend-api:${APP_ENV}` (`development`, `testing`, `production`). |
| `ASPNETCORE_ENVIRONMENT` | `ASPNETCORE_ENVIRONMENT` | No *(Default: `Production`)* | Define el entorno de .NET (`Development`, `Testing`, `Production`). En `Development` expone el documento OpenAPI (`/openapi/v1.json`). |
| `TZ` | `TZ` | No *(Default: `America/Mexico_City`)* | Zona horaria del contenedor. |
| `KC_HOSTNAME` + `KC_REALM` | `Oidc__Authority` | Sí | Issuer público esperado en el claim `iss` del token JWT (ej. `http://localhost/auth/realms/laboratorio`). |
| `KC_HTTP_RELATIVE_PATH` + `KC_REALM` | `Oidc__MetadataAddress` | Sí | URL interna en la red Docker `backend` para descargar la configuración OIDC y llaves JWKS de Keycloak (`http://keycloak:8080/auth/realms/laboratorio/.well-known/openid-configuration`). |
| `KC_API_CLIENT_ID` | `Oidc__Audience` | No *(Default: `backend-api`)* | Audiencia (`aud`) requerida en los access tokens emitidos por Keycloak. |
| `LEGACY_DB_CONNECTION` *(o `BACKEND_DB_CONNECTION`)* | `ConnectionStrings__DefaultConnection` | Sí | Cadena de conexión a SQL Server (`Laboratory`) usada por `SqlConnectionFactory`. |
| `WWI_DB_CONNECTION` | `ConnectionStrings__WideWorldImporters` | Sí | Cadena de conexión dedicada a `WideWorldImporters` usada por `WwiSqlConnectionFactory` y `SalesReadRepository` (extracción de `/legacy/LEgFrontNEWBACK`). |

---

## 4. Arranque del Contenedor por Ambiente

### Prerrequisito único en el host
Asegúrate de que exista la red Docker externa `backend` (compartida con el Gateway YARP y Keycloak de `07_reverse/01_deployment`) y que exista tu archivo `.env.<ambiente>` en `07_reverse/01_deployment/`:

```bash
# Crear la red compartida (solo una vez por host si aún no la creó 07_reverse)
docker network create backend
```

### Comandos de Arranque (desde la carpeta `05_backend`)

#### Ambiente `development`
```bash
cd 05_backend
docker compose --env-file ../07_reverse/01_deployment/.env.development up -d --build
```

#### Ambiente `testing`
```bash
cd 05_backend
docker compose --env-file ../07_reverse/01_deployment/.env.testing up -d --build
```

#### Ambiente `production`
```bash
cd 05_backend
docker compose --env-file ../07_reverse/01_deployment/.env.production up -d --build
```

### Operaciones Comunes (Logs, Estado y Parada)

```bash
# Ver el estado y salud (healthcheck) del contenedor
docker compose --env-file ../07_reverse/01_deployment/.env.development ps

# Ver logs en tiempo real
docker compose --env-file ../07_reverse/01_deployment/.env.development logs -f backend-api

# Detener el contenedor de este ambiente (no borra la red externa "backend")
docker compose --env-file ../07_reverse/01_deployment/.env.development down
```

---

## 5. Endpoints Disponibles

| Método | Ruta | Autenticación | Descripción |
| :--- | :--- | :--- | :--- |
| `GET` | `/health` | Anónimo | Healthcheck del contenedor (usado por Docker y el Gateway). Devuelve `200 OK` (`Healthy`). |
| `GET` | `/api/system/me` | `Bearer <JWT Keycloak>` | Valida el token contra Keycloak, transforma los roles de `realm_access` / `resource_access` (`admin`, `usuario`, `invitado`) y retorna el `CurrentUserResponse`. |
| `GET` | `/api/sales/customer-transactions` | `Bearer` + política `Lectura` | Consulta paginada (`startDate`, `endDate`, `page=1`, `pageSize=50`) de `WideWorldImporters.Sales.CustomerTransactions`. |
| `GET` | `/api/sales/invoice-lines` | `Bearer` + política `Lectura` | Consulta paginada (`startDate`, `endDate`, `page=1`, `pageSize=50`) de `WideWorldImporters.Sales.InvoiceLines`. |
| `GET` | `/api/sales/invoices` | `Bearer` + política `Lectura` | Consulta paginada (`startDate`, `endDate`, `page=1`, `pageSize=50`) de `WideWorldImporters.Sales.Invoices`. |
| `GET` | `/api/sales/order-lines` | `Bearer` + política `Lectura` | Consulta paginada (`startDate`, `endDate`, `page=1`, `pageSize=50`) de `WideWorldImporters.Sales.OrderLines`. |
| `GET` | `/api/sales/orders` | `Bearer` + política `Lectura` | Consulta paginada (`startDate`, `endDate`, `page=1`, `pageSize=50`) de `WideWorldImporters.Sales.Orders`. |
| `GET` | `/openapi/v1.json` | Anónimo *(Solo `Development`)* | Especificación OpenAPI 3 nativa de .NET 10. |

Políticas de autorización (`Infrastructure/DependencyInjection.cs`), sobre los roles de realm que
`KeycloakClaimsTransformation` saca de `realm_access`:

| Política | Roles | Uso |
| :--- | :--- | :--- |
| `Lectura` | `usuario`, `admin`, `invitado` | `/api/sales/*`. Sin token 401; con token sin ninguno de los roles, 403. |
| `UsuarioOrAdmin` | `usuario`, `admin` | Reservada para operaciones que modifiquen datos. |
| `AdminOnly` | `admin` | Reservada para administración. |

Desde fuera de Docker los endpoints `/api/*` se alcanzan por nginx + gateway (`https://<host>/api/...`, ruta
`backend-api-route` de `07_reverse/01_deployment/Gateway`). El primer consumidor es
`02_webapp_webforms/.../LEgFrontNEWBACK.aspx`, que llama desde el servidor con el token del usuario
(cliente `legacy-webforms`, que emite `aud: backend-api`).

---

## 6. Buena Práctica Futura: Comunicación con Otros Microservicios (Token Exchange RFC 8693)

Cuando un caso de uso en `Application` necesite consumir otro microservicio interno:
1. Se define la interfaz del cliente en `Application/Common/Abstractions/`.
2. Se implementa el adaptador HTTP en `Infrastructure/Microservices/` registrando el [`TokenExchangeDelegatingHandler`](src/Infrastructure/Microservices/TokenExchangeDelegatingHandler.cs).
3. Este handler intercambia automáticamente el token del usuario actual contra Keycloak (`grant_type=urn:ietf:params:oauth:grant-type:token-exchange`) por un token con el `audience` específico del microservicio destino.
