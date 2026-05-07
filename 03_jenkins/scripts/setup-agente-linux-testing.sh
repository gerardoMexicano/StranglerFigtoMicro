#!/bin/bash
# =============================================================================
# setup-agente-linux-testing.sh
# Agente: agente-linux-testing | Labels: linux testing
# El secret se obtiene AUTOMÁTICAMENTE via Jenkins API
# Uso: JENKINS_URL=http://jenkins:8080 JENKINS_USER=admin JENKINS_TOKEN=xxxxx ./script.sh
# =============================================================================

set -euo pipefail

RED='\033[0;31m'; GREEN='\033[0;32m'; YELLOW='\033[1;33m'; BLUE='\033[0;34m'; NC='\033[0m'
info()    { echo -e "${BLUE}[INFO]${NC}  $*"; }
success() { echo -e "${GREEN}[OK]${NC}    $*"; }
warn()    { echo -e "${YELLOW}[WARN]${NC}  $*"; }
error()   { echo -e "${RED}[ERROR]${NC} $*"; exit 1; }

# ── Parámetros ────────────────────────────────────────────────────────────────
AGENT_NAME="agente-linux-testing"
AGENT_LABELS="linux testing"
REMOTE_FS="/home/jenkins"
NUM_EXECUTORS=2
JAVA_MIN_VERSION=11

JENKINS_URL="${JENKINS_URL:-}"
JENKINS_USER="${JENKINS_USER:-}"
JENKINS_TOKEN="${JENKINS_TOKEN:-}"      # API Token: Jenkins → Tu usuario → Configure → API Token

[[ -z "$JENKINS_URL"   ]] && error "Falta JENKINS_URL.   Ej: export JENKINS_URL=http://jenkins:8080"
[[ -z "$JENKINS_USER"  ]] && error "Falta JENKINS_USER.  Ej: export JENKINS_USER=admin"
[[ -z "$JENKINS_TOKEN" ]] && error "Falta JENKINS_TOKEN. Obtén el token en Jenkins → User → Configure → API Token"

JENKINS_URL="${JENKINS_URL%/}"

# ── 1. VERIFICAR / INSTALAR JAVA ──────────────────────────────────────────────
echo ""
info "=== Paso 1: Verificando Java ==="

install_java() {
    if   command -v apt-get &>/dev/null; then
        sudo apt-get update -qq && sudo apt-get install -y "openjdk-${JAVA_MIN_VERSION}-jre-headless"
    elif command -v dnf     &>/dev/null; then
        sudo dnf install -y "java-${JAVA_MIN_VERSION}-openjdk-headless"
    elif command -v yum     &>/dev/null; then
        sudo yum install -y "java-${JAVA_MIN_VERSION}-openjdk-headless"
    else
        error "No se encontró apt-get, dnf ni yum. Instala Java ${JAVA_MIN_VERSION}+ manualmente."
    fi
}

if command -v java &>/dev/null; then
    JAVA_VER=$(java -version 2>&1 | awk -F '"' '/version/{print $2}' | cut -d'.' -f1)
    [[ "$JAVA_VER" == "1" ]] && JAVA_VER=$(java -version 2>&1 | awk -F '"' '/version/{print $2}' | cut -d'.' -f2)
    if [[ "$JAVA_VER" -ge "$JAVA_MIN_VERSION" ]]; then
        success "Java ${JAVA_VER} OK"
    else
        warn "Java ${JAVA_VER} insuficiente, instalando Java ${JAVA_MIN_VERSION}..."
        install_java && success "Java instalado"
    fi
else
    warn "Java no encontrado, instalando..."
    install_java && success "Java instalado"
fi

# ── 2. PREPARAR USUARIO Y DIRECTORIO ─────────────────────────────────────────
echo ""
info "=== Paso 2: Preparando usuario y directorio ==="

if ! id -u jenkins &>/dev/null; then
    sudo useradd -r -m -d "$REMOTE_FS" -s /bin/bash jenkins
    success "Usuario 'jenkins' creado"
else
    success "Usuario 'jenkins' ya existe"
fi

sudo mkdir -p "${REMOTE_FS}/remoting"
sudo chown -R jenkins:jenkins "$REMOTE_FS"
success "Directorio ${REMOTE_FS} listo"

# ── 3. CREAR NODO EN JENKINS VIA API ─────────────────────────────────────────
echo ""
info "=== Paso 3: Registrando nodo en Jenkins via API ==="

NODE_XML='<slave>
  <name>'"${AGENT_NAME}"'</name>
  <description>Agente Linux - Testing</description>
  <remoteFS>'"${REMOTE_FS}"'</remoteFS>
  <numExecutors>'"${NUM_EXECUTORS}"'</numExecutors>
  <mode>EXCLUSIVE</mode>
  <retentionStrategy class="hudson.slaves.RetentionStrategy$Always"/>
  <launcher class="hudson.slaves.JNLPLauncher">
    <workDirSettings>
      <disabled>false</disabled>
      <internalDir>remoting</internalDir>
      <failIfWorkDirIsMissing>false</failIfWorkDirIsMissing>
    </workDirSettings>
  </launcher>
  <label>'"${AGENT_LABELS}"'</label>
  <nodeProperties/>
</slave>'

# Obtener crumb CSRF
info "Obteniendo crumb CSRF..."
CRUMB_JSON=$(curl -fsSL -u "${JENKINS_USER}:${JENKINS_TOKEN}" \
    "${JENKINS_URL}/crumbIssuer/api/json" 2>/dev/null) || true

if [[ -n "$CRUMB_JSON" && "$CRUMB_JSON" != *"404"* ]]; then
    CRUMB_FIELD=$(echo "$CRUMB_JSON" | grep -o '"crumbRequestField":"[^"]*"' | cut -d'"' -f4)
    CRUMB_VALUE=$(echo "$CRUMB_JSON" | grep -o '"crumb":"[^"]*"'             | cut -d'"' -f4)
    CRUMB_HEADER="${CRUMB_FIELD}:${CRUMB_VALUE}"
    success "Crumb obtenido"
else
    warn "Sin CSRF activo, continuando sin crumb"
    CRUMB_HEADER="x-no-crumb:true"
fi

# Verificar si el nodo ya existe
HTTP_CHECK=$(curl -o /dev/null -w "%{http_code}" -fsSL \
    -u "${JENKINS_USER}:${JENKINS_TOKEN}" \
    "${JENKINS_URL}/computer/${AGENT_NAME}/api/json" 2>/dev/null) || HTTP_CHECK="000"

if [[ "$HTTP_CHECK" == "200" ]]; then
    warn "Nodo '${AGENT_NAME}' ya existe en Jenkins, se omite la creación"
else
    info "Creando nodo '${AGENT_NAME}' en Jenkins..."
    HTTP_STATUS=$(curl -o /dev/null -w "%{http_code}" -fsSL \
        -u "${JENKINS_USER}:${JENKINS_TOKEN}" \
        -H "$CRUMB_HEADER" \
        -H "Content-Type: application/xml" \
        --data-raw "$NODE_XML" \
        "${JENKINS_URL}/computer/doCreateItem?name=${AGENT_NAME}&type=hudson.slaves.DumbSlave")

    [[ "$HTTP_STATUS" =~ ^(200|302)$ ]] \
        && success "Nodo creado (HTTP ${HTTP_STATUS})" \
        || error "Error al crear nodo (HTTP ${HTTP_STATUS}). Verifica credenciales y permisos."
fi

# ── 4. OBTENER EL SECRET AUTOMÁTICAMENTE ──────────────────────────────────────
echo ""
info "=== Paso 4: Obteniendo secret del agente ==="

JENKINS_SECRET=""
for i in {1..10}; do
    JNLP_RESPONSE=$(curl -fsSL \
        -u "${JENKINS_USER}:${JENKINS_TOKEN}" \
        "${JENKINS_URL}/computer/${AGENT_NAME}/slave-agent.jnlp" 2>/dev/null) || true

    JENKINS_SECRET=$(echo "$JNLP_RESPONSE" | grep -oP '(?<=-secret )[a-f0-9]{64}' | head -1)

    [[ -n "$JENKINS_SECRET" ]] && { success "Secret obtenido"; break; }

    warn "Intento ${i}/10 — esperando que Jenkins genere el secret (3s)..."
    sleep 3
done

[[ -z "$JENKINS_SECRET" ]] && \
    error "No se pudo obtener el secret. Verifica que '${JENKINS_USER}' tenga permiso 'Agent/Connect'."

# ── 5. DESCARGAR agent.jar ────────────────────────────────────────────────────
echo ""
info "=== Paso 5: Descargando agent.jar ==="

AGENT_JAR="${REMOTE_FS}/agent.jar"
sudo -u jenkins curl -fsSL --retry 5 --retry-delay 5 \
    -o "$AGENT_JAR" "${JENKINS_URL}/jnlpJars/agent.jar" \
    || error "No se pudo descargar agent.jar"
sudo chmod 644 "$AGENT_JAR"
success "agent.jar en ${AGENT_JAR}"

# ── 6. CREAR Y ARRANCAR SERVICIO SYSTEMD ─────────────────────────────────────
echo ""
info "=== Paso 6: Creando servicio systemd ==="

sudo tee /etc/systemd/system/jenkins-agent-testing.service > /dev/null <<EOF
[Unit]
Description=Jenkins Agent - ${AGENT_NAME}
After=network-online.target
Wants=network-online.target

[Service]
Type=simple
User=jenkins
WorkingDirectory=${REMOTE_FS}
Environment="JAVA_OPTS=-Xmx512m"

ExecStart=/usr/bin/java \
    -jar ${AGENT_JAR} \
    -url ${JENKINS_URL} \
    -secret ${JENKINS_SECRET} \
    -name ${AGENT_NAME} \
    -workDir ${REMOTE_FS}

Restart=on-failure
RestartSec=15
StartLimitIntervalSec=120
StartLimitBurst=3
LimitNOFILE=32768
LimitNPROC=16384
StandardOutput=journal
StandardError=journal
SyslogIdentifier=jenkins-agent-testing

[Install]
WantedBy=multi-user.target
EOF

sudo systemctl daemon-reload
sudo systemctl enable --now jenkins-agent-testing.service

sleep 3
systemctl is-active --quiet jenkins-agent-testing.service \
    && success "Agente iniciado y conectado a Jenkins" \
    || error "El servicio falló. Revisa: journalctl -u jenkins-agent-testing -n 50"

# ── RESUMEN ───────────────────────────────────────────────────────────────────
echo ""
echo -e "${GREEN}════════════════════════════════════════════════${NC}"
echo -e "${GREEN}  Agente listo: ${AGENT_NAME}${NC}"
echo -e "${GREEN}════════════════════════════════════════════════${NC}"
echo -e "  Labels    : ${AGENT_LABELS}"
echo -e "  RemoteFS  : ${REMOTE_FS}"
echo -e "  Executors : ${NUM_EXECUTORS}"
echo -e "  Secret    : ${JENKINS_SECRET:0:12}... (obtenido via API)"
echo -e "  Servicio  : jenkins-agent-testing"
echo ""
echo -e "  Ver logs  : journalctl -u jenkins-agent-testing -f"
echo -e "  Estado    : systemctl status jenkins-agent-testing"
echo -e "${GREEN}════════════════════════════════════════════════${NC}"
