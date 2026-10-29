#!/usr/bin/env bash
# Logout global (front-channel) de las apps legacy: legacy-mvc y legacy-webforms.
#
# Al cerrar la sesión de Keycloak desde cualquier app, Keycloak carga en un iframe la
# frontchannel.logout.url de cada cliente con sesión (?iss=...&sid=...) y la app borra su cookie.
#   aplicar  frontchannelLogout=true, URL de logout y session.required (manda iss y sid)
#   quitar   deja la URL vacía: Keycloak ya no avisa a las apps (como antes)
#   ver      solo muestra la configuración
# Las URLs salen de IMPORT_LEGACY_*_URL (las mismas que importa el realm). El realm se importa con
# IGNORE_EXISTING: en un Keycloak ya levantado este script es el que aplica el cambio.
#
# Publicar antes las apps con su endpoint (Account/FrontchannelLogout y FrontchannelLogout.ashx):
# sin él, el iframe da 404 y la app no se entera (no rompe nada más).
#
# Uso (desde 07_reverse/01_deployment):
#   ./keycloak/scripts/logout-global.sh development ver
#   ./keycloak/scripts/logout-global.sh development aplicar
#   ./keycloak/scripts/logout-global.sh development quitar
#
# Usa el admin de KC_BOOTSTRAP_ADMIN_* del contenedor. Repetirlo no cambia nada.
set -euo pipefail

AMBIENTE="${1:?Uso: $0 <development|testing|production> <ver|aplicar|quitar>}"
MODO="${2:?Uso: $0 <development|testing|production> <ver|aplicar|quitar>}"
case "$MODO" in ver|aplicar|quitar) ;; *) echo "Modo inválido: $MODO (ver|aplicar|quitar)" >&2; exit 1 ;; esac

cd "$(dirname "$0")/../.."

docker compose --env-file ".env.${AMBIENTE}" exec -T -e MODO="$MODO" keycloak bash -s <<'EOF'
set -euo pipefail
KCADM="/opt/keycloak/bin/kcadm.sh"
CFG="--config /tmp/kcadm-logout.config"
trap 'rm -f /tmp/kcadm-logout.config' EXIT

$KCADM config credentials $CFG \
  --server "http://localhost:8080${KC_HTTP_RELATIVE_PATH}" \
  --realm master \
  --user "$KC_BOOTSTRAP_ADMIN_USERNAME" \
  --password "$KC_BOOTSTRAP_ADMIN_PASSWORD" 2>/dev/null

# cliente|URL de su endpoint de logout
CLIENTES=(
  "legacy-mvc|$IMPORT_LEGACY_MVC_URL/Account/FrontchannelLogout"
  "legacy-webforms|$IMPORT_LEGACY_WEBFORMS_URL/FrontchannelLogout.ashx"
)

mostrar() {
  echo "== $CLIENTE ($1)"
  $KCADM get "clients/$ID" $CFG -r "$IMPORT_REALM" \
    --fields 'frontchannelLogout,attributes(frontchannel.logout.url,frontchannel.logout.session.required)' \
    | tr -d '\n' | sed 's/  */ /g'
  echo
}

for ENTRADA in "${CLIENTES[@]}"; do
  CLIENTE="${ENTRADA%%|*}"
  URL="${ENTRADA#*|}"
  ID=$($KCADM get clients $CFG -r "$IMPORT_REALM" -q clientId="$CLIENTE" --fields id --format csv --noquotes)
  if [ -z "$ID" ]; then
    echo "No existe el cliente $CLIENTE en el realm $IMPORT_REALM" >&2
    exit 1
  fi

  if [ "$MODO" = ver ]; then mostrar "actual"; continue; fi
  mostrar "antes"

  if [ "$MODO" = aplicar ]; then
    $KCADM update "clients/$ID" $CFG -r "$IMPORT_REALM" \
      -s frontchannelLogout=true \
      -s "attributes.\"frontchannel.logout.url\"=$URL" \
      -s 'attributes."frontchannel.logout.session.required"=true'
  else
    $KCADM update "clients/$ID" $CFG -r "$IMPORT_REALM" -s 'attributes."frontchannel.logout.url"='
  fi
  mostrar "después"
done
EOF
