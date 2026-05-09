export JENKINS_URL="http://tu-jenkins:8080"
export JENKINS_USER="admin"
export JENKINS_TOKEN="tu-api-token"   # NO es tu contraseña  no guardes tu token

chmod +x setup-agente-linux-production.sh #  puedes seleccionar cual o cuales quieres ejecutar 
sudo -E ./setup-agente-linux-production.sh
#sudo -E ./setup-agente-linux-testing

