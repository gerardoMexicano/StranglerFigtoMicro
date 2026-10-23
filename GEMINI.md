# Reglas del Proyecto y Arquitectura (GEMINI.md)

## 1. Contexto General y Alcance
Este repositorio contiene la modernización progresiva de aplicaciones legadas (.NET Framework 4.8 MVC y WebForms) hacia una arquitectura moderna basada en microservicios utilizando el patrón **Strangler Fig**.
- **Backend (`05_backend`):** .NET 10 (`net10.0`) bajo **Clean Architecture**, ejecutándose 100% en contenedores **Docker (Kestrel sobre Linux)** sin ningún acoplamiento a IIS.
- **Arranque y Configuración por Ambiente (`.env` centralizado):**
  - El arranque de `05_backend` es independiente (propio `05_backend/docker-compose.yml` y `05_backend/Dockerfile`), conectado a la red Docker externa `backend`.
  - Consume las variables de entorno definidas en `07_reverse/01_deployment/.env.<ambiente>` (`.env.development`, `.env.testing`, `.env.production` basados en `template.env.deployment`).
  - La imagen/contenedor es inmutable entre ambientes; el comportamiento varía únicamente según el `--env-file` inyectado al arrancar.
- **Identidad y Accesos (IAM):** Delegada completamente a **Keycloak** (OpenID Connect / OAuth 2.0). Este backend actúa como un **Resource Server**:
  - Valida el `iss` público con `Oidc:Authority` (`${KC_HOSTNAME}/realms/${KC_REALM}`).
  - Descubre las llaves JWKS por la red interna Docker `backend` usando `Oidc:MetadataAddress` (`http://keycloak:8080${KC_HTTP_RELATIVE_PATH}/realms/${KC_REALM}/.well-known/openid-configuration`).
  - Valida el `aud` con `Oidc:Audience` (`${KC_API_CLIENT_ID}`, por defecto `backend-api`) y mapea los roles del realm (`admin`, `usuario`, `invitado`).
- **Comunicación futura entre Microservicios (Buena Práctica):** Cuando este backend requiera invocar a otros microservicios, el estándar adoptado es **OAuth 2.0 Token Exchange (RFC 8693)** contra Keycloak (`grant_type=urn:ietf:params:oauth:grant-type:token-exchange`), intercambiando el token entrante del usuario por un token acotado al `audience` del microservicio destino mediante un `DelegatingHandler` en `Infrastructure`.

---

## 2. Metodología Obligatoria: TDD con Intervención (Test-Driven Development)

Para toda nueva funcionalidad o módulo a migrar, es **obligatorio** seguir el ciclo **TDD (Red → Revisión/Intervención → Green → Refactor)**:

1. **Paso 1 — Generación de Pruebas Primero (Fase Red + Intervención):**
   - Antes de implementar el código definitivo de negocio/infraestructura, se deben escribir primero los casos de prueba en `05_backend/tests/UnitTests` y/o `05_backend/tests/IntegrationTests` (junto con los contratos/interfaces mínimos necesarios para expresar la prueba).
   - **Punto de Intervención Obligatorio:** Se presentan las pruebas diseñadas (casos de éxito, errores de dominio, validaciones y reglas de autorización) al usuario para su revisión, ajuste o aprobación explícita.
2. **Paso 2 — Implementación del Código Definitivo (Fase Green):**
   - Una vez aprobadas las pruebas (y realizada cualquier intervención sobre ellas), se implementa el código definitivo en `Domain`, `Application`, `Infrastructure` y `Api` hasta lograr que `dotnet test` pase al 100% en verde.
3. **Paso 3 — Refactorización (Fase Refactor):**
   - Se optimiza y limpia el código respetando las reglas de Clean Architecture y verificando que ninguna prueba se rompa.

---

## 3. Reglas Estrictas de Clean Architecture (`05_backend`)

La solución `Migracion.slnx` se divide en 4 capas principales dentro de `05_backend/src/` y proyectos de prueba en `05_backend/tests/`. **La Regla de Dependencia es inquebrantable: las dependencias solo apuntan hacia adentro (hacia `Domain`).**

### 3.1. `Domain` (Núcleo Empresarial)
- **Responsabilidad:** Entidades de dominio, Value Objects, Enumeraciones, Excepciones de Dominio, Eventos de Dominio y primitivas base (`Result`, `Result<T>`, `Error`, `ErrorType`).
- **Restricciones:**
  - **Cero dependencias externas** (prohibido referenciar paquetes NuGet de infraestructura, ORM, HTTP o serializadores externos).
  - Entidades ricas con encapsulamiento cuando exista lógica de negocio.

### 3.2. `Application` (Casos de Uso y Orquestación)
- **Responsabilidad:**
  - **CQRS con Abstracciones Propias (Zero-Third-Party Paid Dependencies):** Uso de interfaces propias `ICommand<TResponse>`, `ICommandHandler<TCommand, TResponse>`, `IQuery<TResponse>` e `IQueryHandler<TQuery, TResponse>` (sin `MediatR` ni librerías con licencias comerciales).
  - Decoradores / Pipelines ligeros para validación (`FluentValidation`) y logging.
  - DTOs (`record`), Interfaces de Puertos (Repositorios, `ICurrentUserService`, `IDateTimeProvider`, y clientes de microservicios externos).
- **Dependencias permitidas:** Únicamente `Domain` (y paquetes puros de abstracción/validación como `FluentValidation` o `Microsoft.Extensions.Logging.Abstractions`).
- **Restricciones:**
  - Prohibido conocer detalles de base de datos (SQL, `SqlConnection`, Dapper), detalles HTTP (`HttpContext`, `HttpClient`) o configuración de Keycloak.

### 3.3. `Infrastructure` (Adaptadores Externos y Persistencia)
- **Responsabilidad:** Implementación de las interfaces de `Application` y `Domain`:
  - **Persistencia con Dapper:** Acceso a datos simple, rápido y explícito usando **Dapper** + `Microsoft.Data.SqlClient` a través de una factoría `IDbConnectionFactory` (`ConnectionStrings:DefaultConnection`).
  - **Identidad / Seguridad (Keycloak):** Configuración `OidcOptions` (`Oidc:Authority`, `Oidc:MetadataAddress`, `Oidc:Audience`), extracción de claims del token (`ICurrentUserService`) y transformación de roles (`KeycloakClaimsTransformation` para mapear `realm_access` y `resource_access` a roles estándar).
  - **Integración con Microservicios (Preparado para el futuro):** Clientes HTTP tipados (`IHttpClientFactory`, resiliencia con `Microsoft.Extensions.Http.Resilience`) y soporte para **Token Exchange (RFC 8693)** vía `DelegatingHandler`.
- **Dependencias permitidas:** `Application` y `Domain`.
- **Restricciones:** No debe contener lógica de negocio ni reglas de dominio.

### 3.4. `Api` (Presentación / Entry Point)
- **Responsabilidad:**
  - **Minimal APIs** organizadas de forma modular (implementando `IEndpoint` / grupos de rutas con `MapGroup`) + **OpenAPI nativo de .NET 10** (`Microsoft.AspNetCore.OpenApi`).
  - Ejecución pura en Kestrel (sin dependencias ni configuraciones de IIS).
  - Configuración de Inyección de Dependencias (`Program.cs`), Autenticación `JwtBearer` contra el contrato `Oidc__*` de Keycloak, Autorización basada en Políticas/Roles, `ProblemDetails` (`IExceptionHandler` global) y Health Checks (`/health`).
- **Dependencias permitidas:** `Application` e `Infrastructure` (solo para el registro de DI en el Composition Root).
- **Restricciones:**
  - Los Minimal API endpoints **no** deben contener lógica de negocio ni consultas SQL/Dapper; solo reciben la petición, inyectan el `ICommandHandler` o `IQueryHandler` correspondiente, y transforman el `Result<T>` en `IResult` (`TypedResults` / `ProblemDetails` RFC 7807).

---

## 4. Estándares de Código y Despliegue en .NET 10
- **C# moderno:** Uso de *Primary Constructors*, `record` para DTOs, Commands y Queries inmutables, `Nullable Reference Types` habilitado (`<Nullable>enable</Nullable>`), y colecciones `IReadOnlyList<T>` en respuestas.
- **Asincronía:** Todas las operaciones I/O (Dapper `CommandDefinition` con `CancellationToken`, llamadas HTTP) deben ser `async/await` y propagar `CancellationToken` desde el Minimal API endpoint hasta el repositorio.
- **Contenedorización y Variables de Entorno:**
  - Nada de secretos ni URLs hardcodeadas en `appsettings.json`. Todo valor sensible o dependiente del entorno se inyecta por variables de entorno desde `07_reverse/01_deployment/.env.<ambiente>` y se valida al inicio con `ValidateDataAnnotations().ValidateOnStart()`.
  - Mantener siempre actualizado `05_backend/README.md` con la tabla de variables de entorno y los comandos exactos de arranque por ambiente (`development`, `testing`, `production`).
