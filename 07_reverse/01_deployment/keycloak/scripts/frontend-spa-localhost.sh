#!/usr/bin/env bash
# Permite (o deja de permitir) que `ng serve` en http://localhost:4200 entre por el cliente
# frontend-spa: redirect URI, web origin (CORS del token endpoint) y post logout redirect URI.
#
# Solo development: se niega con cualquier otro ambiente y revisa que el env file sea de
# development (APP_ENV=development, KC_COMMAND=start-dev). El realm se importa con
# IGNORE_EXISTING, así que esto no está en laboratorio-realm.json: se aplica a mano y se quita.
#
# Uso (desde 07_reverse/01_deployment):
#   ./keycloak/scripts/frontend-spa-localhost.sh development ver       # solo muestra las URIs
#   ./keycloak/scripts/frontend-spa-localhost.sh development agregar
#   ./keycloak/scripts/frontend-spa-localhost.sh development quitar
#
# Usa el admin de KC_BOOTSTRAP_ADMIN_* del contenedor. No toca otras URIs del cliente y repetirlo
# no duplica nada.
set -euo pipefail

AMBIENTE="${1:?Uso: $0 development <ver|agregar|quitar>}"
MODO="${2:?Uso: $0 development <ver|agregar|quitar>}"
ORIGEN="http://localhost:4200"

if [ "$AMBIENTE" != "development" ]; then
  echo "Solo para development: no se agrega localhost a frontend-spa en $AMBIENTE" >&2
  exit 1
fi
case "$MODO" in ver|agregar|quitar) ;; *) echo "Modo inválido: $MODO (ver|agregar|quitar)" >&2; exit 1 ;; esac

cd "$(dirname "$0")/../.."
ENV_FILE=".env.${AMBIENTE}"
if ! grep -Eq '^APP_ENV=development\s*$' "$ENV_FILE" || ! grep -Eq '^KC_COMMAND=start-dev\s*$' "$ENV_FILE"; then
  echo "$ENV_FILE no es de development (APP_ENV=development, KC_COMMAND=start-dev)" >&2
  exit 1
fi

docker compose --env-file "$ENV_FILE" exec -T -e MODO="$MODO" -e ORIGEN="$ORIGEN" keycloak bash -s <<'EOF'
set -euo pipefail
KCADM="/opt/keycloak/bin/kcadm.sh"
CFG="--config /tmp/kcadm-localhost.config"
CLIENTE="$IMPORT_FRONTEND_CLIENT_ID"
trap 'rm -f /tmp/kcadm-localhost.config' EXIT

$KCADM config credentials $CFG \
  --server "http://localhost:8080${KC_HTTP_RELATIVE_PATH}" \
  --realm master \
  --user "$KC_BOOTSTRAP_ADMIN_USERNAME" \
  --password "$KC_BOOTSTRAP_ADMIN_PASSWORD" 2>/dev/null

ID=$($KCADM get clients $CFG -r "$IMPORT_REALM" -q clientId="$CLIENTE" --fields id --format csv --noquotes)
if [ -z "$ID" ]; then
  echo "No existe el cliente $CLIENTE en el realm $IMPORT_REALM" >&2
  exit 1
fi

# Una URI por línea. kcadm imprime los arreglos como [ "a", "b" ]; el post logout es un
# atributo de texto con las URIs separadas por "##".
arreglo() {
  $KCADM get "clients/$ID" $CFG -r "$IMPORT_REALM" --fields "$1" | tr -d '\n' \
    | sed -e 's/^[^[]*\[//' -e 's/\].*$//' | grep -o '"[^"]*"' | tr -d '"' || true
}
post_logout() {
  $KCADM get "clients/$ID" $CFG -r "$IMPORT_REALM" --fields 'attributes(post.logout.redirect.uris)' | tr -d '\n' \
    | sed -n 's/.*"post.logout.redirect.uris" *: *"\([^"]*\)".*/\1/p' | sed 's/##/\n/g' | grep -v '^$' || true
}
# agregar: la lista + el valor si no estaba; quitar: la lista sin el valor
cambiar() {
  if [ "$MODO" = agregar ]; then
    { printf '%s\n' "$1"; grep -qxF "$2" <<<"$1" || printf '%s\n' "$2"; } | grep -v '^$' || true
  else
    grep -vxF "$2" <<<"$1" || true
  fi
}
json() {
  if [ -z "$1" ]; then echo '[]'; else sed 's/.*/"&"/' <<<"$1" | paste -sd, | sed 's/^/[/; s/$/]/'; fi
}

mostrar() {
  echo "== $CLIENTE ($1)"
  echo "redirectUris:              $(arreglo redirectUris | paste -sd' ')"
  echo "webOrigins:                $(arreglo webOrigins | paste -sd' ')"
  echo "post.logout.redirect.uris: $(post_logout | paste -sd' ')"
}

mostrar "antes"
[ "$MODO" = ver ] && exit 0

REDIRECT=$(cambiar "$(arreglo redirectUris)" "$ORIGEN/*")
ORIGENES=$(cambiar "$(arreglo webOrigins)" "$ORIGEN")
LOGOUT=$(cambiar "$(post_logout)" "$ORIGEN/*")

$KCADM update "clients/$ID" $CFG -r "$IMPORT_REALM" \
  -s "redirectUris=$(json "$REDIRECT")" \
  -s "webOrigins=$(json "$ORIGENES")" \
  -s "attributes.\"post.logout.redirect.uris\"=$(paste -sd'#' <<<"$LOGOUT" | sed 's/#/##/g')"

mostrar "después"
EOF
