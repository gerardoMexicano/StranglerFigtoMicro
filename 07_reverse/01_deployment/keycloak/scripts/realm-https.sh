#!/usr/bin/env bash
# Migración a https de las URIs de los clientes frontend-spa, legacy-mvc y legacy-webforms.
#
# El realm se importa con IGNORE_EXISTING: cambiar APP_SCHEME no cambia un Keycloak ya levantado.
#   agregar     por cada URI http://<dominio>... agrega la misma en https:// y deja la http.
#               Se corre ANTES del corte: con las dos, el login funciona con APP_SCHEME en http o https
#               (y así se puede regresar a http sin tocar Keycloak).
#   quitar-http quita las URIs http://<dominio>... y pasa rootUrl / baseUrl a https://.
#               Solo DESPUÉS del corte: se niega mientras KC_HOSTNAME siga en http://.
#   ver         solo muestra las URIs.
# <dominio> sale de APP_PUBLIC_URL (sin el esquema). Otras URIs (IIS Express, localhost:4200) no se tocan.
#
# Uso (desde 07_reverse/01_deployment):
#   ./keycloak/scripts/realm-https.sh development ver
#   ./keycloak/scripts/realm-https.sh development agregar
#   ./keycloak/scripts/realm-https.sh development quitar-http
#
# Usa el admin de KC_BOOTSTRAP_ADMIN_* del contenedor. Repetirlo no duplica nada.
set -euo pipefail

AMBIENTE="${1:?Uso: $0 <development|testing|production> <ver|agregar|quitar-http>}"
MODO="${2:?Uso: $0 <development|testing|production> <ver|agregar|quitar-http>}"
case "$MODO" in ver|agregar|quitar-http) ;; *) echo "Modo inválido: $MODO (ver|agregar|quitar-http)" >&2; exit 1 ;; esac

cd "$(dirname "$0")/../.."

docker compose --env-file ".env.${AMBIENTE}" exec -T -e MODO="$MODO" keycloak bash -s <<'EOF'
set -euo pipefail
KCADM="/opt/keycloak/bin/kcadm.sh"
CFG="--config /tmp/kcadm-https.config"
CLIENTES=("$IMPORT_FRONTEND_CLIENT_ID" legacy-mvc legacy-webforms)
DOMINIO="${IMPORT_APP_PUBLIC_URL#*://}"
HTTP="http://$DOMINIO"
HTTPS="https://$DOMINIO"
trap 'rm -f /tmp/kcadm-https.config' EXIT

if [ "$MODO" = quitar-http ] && [ "${KC_HOSTNAME#http://}" != "$KC_HOSTNAME" ]; then
  echo "KC_HOSTNAME sigue en http:// ($KC_HOSTNAME): quitar las URIs http dejaría a todos sin login" >&2
  exit 1
fi

$KCADM config credentials $CFG \
  --server "http://localhost:8080${KC_HTTP_RELATIVE_PATH}" \
  --realm master \
  --user "$KC_BOOTSTRAP_ADMIN_USERNAME" \
  --password "$KC_BOOTSTRAP_ADMIN_PASSWORD" 2>/dev/null

# Una URI por línea. kcadm imprime los arreglos como [ "a", "b" ]; el post logout es un
# atributo de texto con las URIs separadas por "##".
arreglo() {
  $KCADM get "clients/$ID" $CFG -r "$IMPORT_REALM" --fields "$1" | tr -d '\n' \
    | sed -e 's/^[^[]*\[//' -e 's/\].*$//' | grep -o '"[^"]*"' | tr -d '"' || true
}
texto() {
  $KCADM get "clients/$ID" $CFG -r "$IMPORT_REALM" --fields "$1" | tr -d '\n' \
    | sed -n "s/.*\"$1\" *: *\"\([^\"]*\)\".*/\1/p"
}
post_logout() {
  $KCADM get "clients/$ID" $CFG -r "$IMPORT_REALM" --fields 'attributes(post.logout.redirect.uris)' | tr -d '\n' \
    | sed -n 's/.*"post.logout.redirect.uris" *: *"\([^"]*\)".*/\1/p' | sed 's/##/\n/g' | grep -v '^$' || true
}
# ¿La URI es del dominio por http? (exacta o seguida de "/", para no tomar http://<dominio>0)
es_http() { [ "$1" = "$HTTP" ] || [ "${1#"$HTTP"/}" != "$1" ]; }
# agregar: la lista + la versión https de cada URI http; quitar-http: la lista sin las http
cambiar() {
  local salida="$1" u nueva
  if [ "$MODO" = agregar ]; then
    while IFS= read -r u; do
      if [ -n "$u" ] && es_http "$u"; then
        nueva="$HTTPS${u#"$HTTP"}"
        grep -qxF "$nueva" <<<"$salida" || salida="$salida"$'\n'"$nueva"
      fi
    done <<<"$1"
    grep -v '^$' <<<"$salida" || true
  else
    while IFS= read -r u; do
      if [ -n "$u" ] && ! es_http "$u"; then printf '%s\n' "$u"; fi
    done <<<"$1"
  fi
}
a_https() { if es_http "$1"; then printf '%s' "$HTTPS${1#"$HTTP"}"; else printf '%s' "$1"; fi; }
json() {
  if [ -z "$1" ]; then echo '[]'; else sed 's/.*/"&"/' <<<"$1" | paste -sd, | sed 's/^/[/; s/$/]/'; fi
}

mostrar() {
  echo "== $CLIENTE ($1)"
  echo "rootUrl:                   $(texto rootUrl)"
  echo "baseUrl:                   $(texto baseUrl)"
  echo "redirectUris:              $(arreglo redirectUris | paste -sd' ')"
  echo "webOrigins:                $(arreglo webOrigins | paste -sd' ')"
  echo "post.logout.redirect.uris: $(post_logout | paste -sd' ')"
}

for CLIENTE in "${CLIENTES[@]}"; do
  ID=$($KCADM get clients $CFG -r "$IMPORT_REALM" -q clientId="$CLIENTE" --fields id --format csv --noquotes)
  if [ -z "$ID" ]; then
    echo "No existe el cliente $CLIENTE en el realm $IMPORT_REALM" >&2
    exit 1
  fi

  if [ "$MODO" = ver ]; then mostrar "actual"; continue; fi
  mostrar "antes"

  REDIRECT=$(cambiar "$(arreglo redirectUris)")
  ORIGENES=$(cambiar "$(arreglo webOrigins)")
  LOGOUT=$(cambiar "$(post_logout)")
  CAMBIOS=(
    -s "redirectUris=$(json "$REDIRECT")"
    -s "webOrigins=$(json "$ORIGENES")"
    -s "attributes.\"post.logout.redirect.uris\"=$(paste -sd'#' <<<"$LOGOUT" | sed 's/#/##/g')"
  )
  # rootUrl / baseUrl son un solo valor: pasan a https solo al quitar las http
  if [ "$MODO" = quitar-http ]; then
    CAMBIOS+=(-s "rootUrl=$(a_https "$(texto rootUrl)")" -s "baseUrl=$(a_https "$(texto baseUrl)")")
  fi

  $KCADM update "clients/$ID" $CFG -r "$IMPORT_REALM" "${CAMBIOS[@]}"
  mostrar "después"
done
EOF
