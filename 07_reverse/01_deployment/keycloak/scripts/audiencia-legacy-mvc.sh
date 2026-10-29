#!/usr/bin/env bash
# Agrega el mapper "audience-api" al cliente legacy-mvc en un realm que ya existe.
#
# El realm se importa con IGNORE_EXISTING: editar laboratorio-realm.json no cambia un Keycloak
# ya levantado. Sin este mapper el access token de MVC (/taxes) no trae "aud: backend-api" y
# 05_backend responde 401.
#
# Uso (desde 07_reverse/01_deployment):
#   ./keycloak/scripts/audiencia-legacy-mvc.sh development
#
# Usa el admin de KC_BOOTSTRAP_ADMIN_* del contenedor. Si ya existe el mapper no hace nada.
set -euo pipefail

AMBIENTE="${1:?Uso: $0 <development|testing|production>}"
cd "$(dirname "$0")/../.."

docker compose --env-file ".env.${AMBIENTE}" exec -T keycloak bash -s <<'EOF'
set -euo pipefail
KCADM="/opt/keycloak/bin/kcadm.sh"
CFG="--config /tmp/kcadm-audiencia-mvc.config"
CLIENTE="legacy-mvc"

$KCADM config credentials $CFG \
  --server "http://localhost:8080${KC_HTTP_RELATIVE_PATH}" \
  --realm master \
  --user "$KC_BOOTSTRAP_ADMIN_USERNAME" \
  --password "$KC_BOOTSTRAP_ADMIN_PASSWORD"

ID=$($KCADM get clients $CFG -r "$IMPORT_REALM" -q clientId="$CLIENTE" --fields id --format csv --noquotes)
if [ -z "$ID" ]; then
  echo "No existe el cliente $CLIENTE en el realm $IMPORT_REALM" >&2
  exit 1
fi

if $KCADM get "clients/$ID/protocol-mappers/models" $CFG -r "$IMPORT_REALM" --fields name --format csv --noquotes \
    | grep -qx "audience-api"; then
  echo "$CLIENTE ya tiene el mapper audience-api"
else
  $KCADM create "clients/$ID/protocol-mappers/models" $CFG -r "$IMPORT_REALM" \
    -s name=audience-api \
    -s protocol=openid-connect \
    -s protocolMapper=oidc-audience-mapper \
    -s consentRequired=false \
    -s "config.\"included.client.audience\"=$IMPORT_API_CLIENT_ID" \
    -s 'config."id.token.claim"=false' \
    -s 'config."access.token.claim"=true' \
    -s 'config."introspection.token.claim"=true'
  echo "Mapper audience-api agregado a $CLIENTE (aud: $IMPORT_API_CLIENT_ID)"
fi

rm -f /tmp/kcadm-audiencia-mvc.config
EOF
