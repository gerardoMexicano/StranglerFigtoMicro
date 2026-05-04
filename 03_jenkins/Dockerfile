FROM jenkins/jenkins:lts

# Instalar plugins
COPY plugins.txt /usr/share/jenkins/ref/plugins.txt
RUN jenkins-plugin-cli -f /usr/share/jenkins/ref/plugins.txt

# Desactivar el asistente de configuración inicial
ENV JAVA_OPTS -Djenkins.install.runSetupWizard=false

# Copiar configuración JCasC
COPY casc/jenkins.yaml /var/jenkins_home/casc_configs/jenkins.yaml

ENV CASC_JENKINS_CONFIG=/var/jenkins_home/casc_configs/jenkins.yaml