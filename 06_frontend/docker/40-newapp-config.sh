#!/bin/sh
# Lo corre el entrypoint de nginx antes de arrancar (/docker-entrypoint.d/).
# Genera /newapp/config.json y las cabeceras de seguridad con las variables del ambiente.
# Si falta una variable obligatoria el contenedor no arranca: mejor eso que una app que no
# puede iniciar sesión.
set -eu

DESTINO=/var/lib/newapp

: "${NEWAPP_OIDC_SCOPE:=openid profile email}"

faltan=""
for var in NEWAPP_OIDC_AUTHORITY NEWAPP_OIDC_CLIENT_ID NEWAPP_OIDC_SCOPE NEWAPP_API_BASE_URL NEWAPP_TAXES_URL NEWAPP_LEGACY_URL; do
    eval "valor=\${$var:-}"
    if [ -z "$valor" ]; then
        faltan="$faltan $var"
    fi
    # Los valores van tal cual dentro de JSON y de una cabecera de nginx
    case "$valor" in
        *\"* | *\\* | *\;*)
            echo "newapp: $var no puede llevar comillas, diagonal invertida ni punto y coma" >&2
            exit 1
            ;;
    esac
done
if [ -n "$faltan" ]; then
    echo "newapp: faltan variables:$faltan" >&2
    exit 1
fi

# Origen (esquema://host[:puerto]) de una URL absoluta; vacío si es relativa (/api)
origen() {
    printf '%s' "$1" | sed -nE 's#^(https?://[^/]+).*#\1#p'
}

NEWAPP_CSP_CONNECT="'self'"
for url in "$NEWAPP_OIDC_AUTHORITY" "$NEWAPP_API_BASE_URL"; do
    o=$(origen "$url")
    if [ -n "$o" ]; then
        NEWAPP_CSP_CONNECT="$NEWAPP_CSP_CONNECT $o"
    fi
done

export NEWAPP_OIDC_SCOPE NEWAPP_CSP_CONNECT

envsubst '${NEWAPP_OIDC_AUTHORITY} ${NEWAPP_OIDC_CLIENT_ID} ${NEWAPP_OIDC_SCOPE} ${NEWAPP_API_BASE_URL} ${NEWAPP_TAXES_URL} ${NEWAPP_LEGACY_URL}' \
    < /etc/newapp/config.json.template > "$DESTINO/config.json"
envsubst '${NEWAPP_CSP_CONNECT}' \
    < /etc/newapp/seguridad.conf.template > "$DESTINO/seguridad.conf"

echo "newapp: config.json generado (authority: $NEWAPP_OIDC_AUTHORITY, api: $NEWAPP_API_BASE_URL)"
