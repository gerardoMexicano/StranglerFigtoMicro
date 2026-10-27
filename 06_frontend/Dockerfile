# ==============================================================================
# Dockerfile Multi-Stage para 06_frontend (Angular 22 servido por nginx en /newapp/)
# La imagen es la misma en todos los ambientes: config.json y las cabeceras de
# seguridad se generan al arrancar con las variables del compose.
# ==============================================================================
FROM node:24-alpine AS build
WORKDIR /src

# 1. Dependencias primero para aprovechar la caché de capas
RUN npm install -g pnpm@10.32.1
COPY ["package.json", "pnpm-lock.yaml", "pnpm-workspace.yaml", "./"]
RUN pnpm install --frozen-lockfile

# 2. Código fuente y build de producción (baseHref /newapp/ en angular.json)
COPY . .
RUN pnpm ng build --configuration production

# 3. nginx sin privilegios (usuario nginx, puerto 8080)
FROM nginxinc/nginx-unprivileged:1.31-alpine AS final

USER root
# Aquí escribe 40-newapp-config.sh al arrancar; el resto de la imagen es de solo lectura para nginx
RUN mkdir -p /var/lib/newapp && chown nginx:nginx /var/lib/newapp

COPY docker/default.conf /etc/nginx/conf.d/default.conf
COPY docker/config.json.template docker/seguridad.conf.template /etc/newapp/
COPY --chmod=755 docker/40-newapp-config.sh /docker-entrypoint.d/40-newapp-config.sh

COPY --from=build /src/dist/frontend/browser /usr/share/nginx/html/newapp
# El config.json de development (public/) no viaja en la imagen
RUN rm -f /usr/share/nginx/html/newapp/config.json

USER nginx
EXPOSE 8080
