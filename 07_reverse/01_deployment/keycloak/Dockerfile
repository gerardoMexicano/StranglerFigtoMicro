# Keycloak + plugin de migración de usuarios legacy (keycloak-user-migration)
# https://github.com/daniel-frak/keycloak-user-migration  (6.x = Keycloak 26.x)
# Al actualizar el plugin, cambiar la URL y el checksum juntos (el build falla si no coinciden).
ARG KEYCLOAK_IMAGE
FROM ${KEYCLOAK_IMAGE}

ADD --chown=keycloak:keycloak \
    --checksum=sha256:54f1b4f8598d7a2b408966cebefdb326226e7f4a4ee4bd87815b0d6753ef5d04 \
    https://github.com/daniel-frak/keycloak-user-migration/releases/download/6.2.4/keycloak-rest-provider-6.2.4.jar \
    /opt/keycloak/providers/keycloak-rest-provider.jar
