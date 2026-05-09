#!/bin/bash

echo "Iniciando el despliegue del proyecto..."

# Detener contenedores previos y limpiar volúmenes huérfanos
docker compose down -v

# Levantar el entorno en modo desatendido
docker compose up -d --build

echo "El proyecto se ha levantado correctamente."
echo "Accediendo a los logs iniciales..."
docker ps
docker compose logs -f --tail=20
