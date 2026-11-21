# 🧪 Laboratorio de migración Strangler Fig — de .NET Framework a .NET 10 + Angular

Laboratorio que simula un caso real: dos aplicaciones heredadas en **.NET Framework 4.8** (ASP.NET MVC y Web Forms sobre IIS) que se modernizan **poco a poco, sin apagarlas**, aplicando el patrón **Strangler Fig**.

Un gateway intercepta todo el tráfico y decide, ruta por ruta (y en la última fase también por rol, en modo *canary*), si una petición la atiende el sistema viejo o el nuevo. Cuando una pantalla ya está migrada, su ruta legacy se retira.

```
navegador ──► nginx (TLS / WAF) ──► Gateway YARP ──┬──► Legacy MVC / Web Forms (IIS)
                     │                             ├──► 05_backend  (.NET 10, Clean Architecture)
                     └──/auth──► Keycloak (OIDC)   └──► 06_frontend (Angular en /newapp)
```

El roadmap completo, paso a paso, está en [`00_docs/roadmap_migracion.md`](00_docs/roadmap_migracion.md).

## Estructura

| Carpeta | Tecnología | Qué contiene |
|---|---|---|
| [`00_docs`](00_docs) | Markdown / Mermaid | Roadmap de la migración, plan de implementación y reportes por fase |
| [`01_webapp_mvc`](01_webapp_mvc) | ASP.NET MVC · .NET Framework 4.8 | Sistema heredado 1 (ya integrado con Keycloak y consumiendo el backend nuevo) |
| [`02_webapp_webforms`](02_webapp_webforms) | ASP.NET Web Forms (VB) · .NET Framework 4.8 | Sistema heredado 2 |
| [`03_jenkins`](03_jenkins) | Jenkins + JCasC + Job DSL · Docker | CI/CD con agentes de *testing* y *production* |
| [`04_sql_auto`](04_sql_auto) | SQL Server 2022 · Docker | Laboratorio de Always On Availability Groups (primario + secundario) |
| [`05_backend`](05_backend) | .NET 10 · Minimal APIs · Dapper | Backend nuevo en Clean Architecture con CQRS propio, pruebas unitarias e integración ([README](05_backend/README.md)) |
| [`06_frontend`](06_frontend) | Angular (standalone, zoneless, signals) | Frontend nuevo que reemplaza pantallas legacy ([README](06_frontend/README.md)) |
| [`07_reverse`](07_reverse) | nginx · YARP · Keycloak · Postgres | Gateway del Strangler Fig, identidad centralizada y migración de usuarios legacy ([README](07_reverse/01_deployment/README.md)) |
| [`sqlserver`](sqlserver) | SQL Server · Docker | Esquema y datos de las bases `Laboratory`, `taxes_proto` y restauración de *WideWorldImporters* |

## Qué se practica aquí

- **Strangler Fig real:** coexistencia legacy/nuevo detrás de un gateway, retiro progresivo de rutas y *canary* por rol.
- **Identidad centralizada:** Keycloak (OIDC) para todas las apps, incluidas las de .NET Framework, con migración transparente de los usuarios del sistema viejo.
- **Clean Architecture + TDD:** dependencias hacia adentro, CQRS sin librerías de pago y `Result<T>` en lugar de excepciones.
- **Expand / contract en base de datos:** cambios de esquema sin romper al sistema heredado (ver `sqlserver/db/03_*` y `04_*`).
- **Un solo artefacto para todos los ambientes:** la misma imagen para development, testing y production; lo único que cambia es el `.env`.
- **Infraestructura:** Docker, Jenkins como código y SQL Server Always On.

## Cómo levantarlo

Cada carpeta tiene su propio `docker-compose.yml` y una plantilla de variables (`template.env.*` / `.template.env.*`). El flujo general es:

1. Copiar la plantilla como `.env` (o `.env.<ambiente>`) y llenar las contraseñas. **Los `.env` nunca se suben al repositorio.**
2. Levantar la base de datos con [`sqlserver`](sqlserver).
3. Levantar gateway + Keycloak con [`07_reverse/01_deployment`](07_reverse/01_deployment/README.md).
4. Levantar [`05_backend`](05_backend/README.md) y [`06_frontend`](06_frontend/README.md).

Las apps legacy (`01_` y `02_`) se abren con **Visual Studio 2022** y corren en IIS / IIS Express con .NET Framework 4.8.

> **Nota sobre credenciales:** todo es un entorno de laboratorio local. Los usuarios y contraseñas que aparecen en los scripts de base de datos, en las cadenas de conexión de las apps legacy y en las pruebas son **datos de demostración**. No se usan en ningún sistema real.
