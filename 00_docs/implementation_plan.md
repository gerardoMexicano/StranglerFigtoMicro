## Resumen Ejecutivo
El objetivo de este plan es establecer la base (Fase 1 / v1) para la migración del monolito .NET Framework utilizando el patrón Strangler Fig. Se ha decidido iniciar migrando el **módulo de Menú / Reportes** debido a su bajo acoplamiento y a que implica lectura/carga de datos.

Se construirán dos microservicios independientes: un **Backend en .NET 10 (Clean Architecture)** y un **Frontend en Angular 22** gestionado con `pnpm`, garantizando las bases de autenticación, logging estructurado, y CI/CD.

## Decisiones Arquitectónicas (Fase 1)
- **Módulo a Migrar:** Módulo de Reportes / Menú.
- **Base de Datos:** Nos conectaremos a la **base de datos existente** del monolito (para carga y lectura).
- **Fachada/Gateway:** Se utilizará **Nginx (ngx)** para el enrutamiento (Strangler Fig).
- **Backend:** .NET 10.
- **Frontend:** Angular 22 usando `pnpm` como gestor de paquetes.

---

## Proposed Changes

La implementación inicial se dividirá en la inicialización de los repositorios/carpetas que acabamos de crear (`05_backend` y `06_frontend`).

---

### Backend (`05_backend`)
Creación de la solución en .NET 10 estructurada en Clean Architecture, configurando herramientas de análisis estático, testing (xUnit) y dependencias core (Swagger, Serilog).

#### [NEW] `05_backend/Migracion.sln`
Solución principal del backend.

#### [NEW] `05_backend/src/Domain/`
Proyecto de biblioteca de clases (`net10.0`). Contendrá Entidades e Interfaces de Repositorios, enfocadas en el módulo de Reportes/Menú.

#### [NEW] `05_backend/src/Application/`
Proyecto de biblioteca de clases (`net10.0`). Contendrá CQRS (MediatR), validaciones (FluentValidation) y DTOs para los reportes y el menú.

#### [NEW] `05_backend/src/Infrastructure/`
Proyecto de biblioteca de clases (`net10.0`). Configuración de Entity Framework Core apuntando a la **base de datos existente**, Serilog.

#### [NEW] `05_backend/src/Api/` (Presentation)
Proyecto Web API (`net10.0`). Configuración de Swagger/OpenAPI, Middleware de manejo de errores, y Autenticación JWT.

#### [NEW] `05_backend/tests/`
Proyectos de pruebas (`UnitTests`, `IntegrationTests`) usando xUnit y Moq.

#### [NEW] `05_backend/.editorconfig`
Reglas estrictas de Roslyn Analyzers y habilitación de Nullable Reference Types.

---

### Frontend (`06_frontend`)
Generación de la aplicación Angular 22 con configuración estricta de TypeScript usando `pnpm`.

#### [NEW] `06_frontend/`
Aplicación Angular base generada vía Angular CLI (`npx -y @angular/cli@22 new frontend --package-manager=pnpm --strict=true`).
Estructura de carpetas a implementar:
- `src/app/core/` (Servicios singleton, guards, interceptors HTTP)
- `src/app/shared/` (Componentes reusables)
- `src/app/features/reports/` (Módulo de reportes a migrar)
- `src/app/features/menu/` (Módulo del menú a migrar)

#### [MODIFY] Configuración de Linter
Ajuste de ESLint y Prettier en el proyecto Angular.

---

## Verification Plan

### Automated Tests
- **Backend:** Se ejecutará `dotnet build` para verificar que no haya warnings. Se ejecutará `dotnet test`.
- **Frontend:** Se ejecutará `pnpm run build` para asegurar compilación exitosa.

### Manual Verification
- Levantar la API del backend y verificar que la interfaz de Swagger esté accesible.
- Levantar el frontend (`pnpm start`) y verificar que la pantalla de inicio cargue correctamente.
