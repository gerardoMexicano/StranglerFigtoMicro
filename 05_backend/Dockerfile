# ==============================================================================
# Dockerfile Multi-Stage para 05_backend (.NET 10 — Clean Architecture)
# Ejecución pura en Kestrel (Linux) sin ningún acoplamiento a IIS.
# ==============================================================================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# 1. Copiar archivos de solución y proyectos para aprovechar caché de capas en restore
COPY ["Migracion.slnx", "./"]
COPY ["src/Domain/Domain.csproj", "src/Domain/"]
COPY ["src/Application/Application.csproj", "src/Application/"]
COPY ["src/Infrastructure/Infrastructure.csproj", "src/Infrastructure/"]
COPY ["src/Api/Api.csproj", "src/Api/"]

RUN dotnet restore "src/Api/Api.csproj"

# 2. Copiar el código fuente y publicar en modo Release
COPY ["src/", "src/"]
WORKDIR "/src/src/Api"
RUN dotnet publish "Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# 3. Imagen final ligera de runtime (Kestrel puro en puerto 8080)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

COPY --from=build /app/publish .

USER $APP_UID
ENTRYPOINT ["dotnet", "Api.dll"]
