# 07_reverse - Estrategias de Reverse Proxy & API Gateway (Strangler Fig)

Este directorio contiene dos implementaciones arquitectónicas completas para el enrutamiento del patrón **Strangler Fig**, permitiendo derivar el tráfico hacia los nuevos microservicios (.NET 10 / Angular 22) o mantenerlo en el monolito legacy (.NET Framework / IIS).

---

## Opciones Disponibles

### [01_nginx_yarp](./01_nginx_yarp) (Nginx + YARP .NET 10)
- **Frontal:** Nginx para terminación TLS, compresión y cacheo.
- **Gateway:** [YARP (Yet Another Reverse Proxy)](https://microsoft.github.io/reverse-proxy/) implementado en .NET 10.
- **Ventaja:** Integración nativa con el ecosistema .NET, configuración declarativa en `appsettings.json`, extensibilidad con middleware de C#.
- **Ejecución:**
  ```bash
  cd 01_nginx_yarp
  docker compose up --build
  ```

### [02_nginx_go_gateway](./02_nginx_go_gateway) (Nginx + Go Custom Gateway)
- **Frontal:** Nginx para balanceo y seguridad perimetral.
- **Gateway:** API Gateway propio implementado en **Go** utilizando `httputil.ReverseProxy`.
- **Ventaja:** Consumo de memoria mínimo (< 15MB RAM), arranque instantáneo (< 10ms), alta concurrencia nativa mediante goroutines, métricas en tiempo real (`/gateway/stats`) y configuración en `config.yaml`.
- **Ejecución:**
  ```bash
  cd 02_nginx_go_gateway
  docker compose up --build
  ```

---

## Matriz Comparativa

| Criterio | Opción 1: Nginx + YARP | Opción 2: Nginx + Go Gateway |
| :--- | :--- | :--- |
| **Tecnología del Gateway** | C# / .NET 10 | Go 1.23 |
| **Configuración de Rutas** | `appsettings.json` | `config.yaml` / Variables de Entorno |
| **Consumo de Memoria** | ~40 - 80 MB | ~8 - 15 MB |
| **Tiempo de Arranque** | ~200 - 500 ms | < 15 ms |
| **Facilidad de Extensión en .NET** | Alta (C# nativo) | Media (Requiere Go) |
| **Observabilidad Integrada** | ASP.NET Core Diagnostics | `/healthz` y `/gateway/stats` nativos |
