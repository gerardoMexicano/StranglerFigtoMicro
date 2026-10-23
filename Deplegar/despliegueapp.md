1. Preparar SSH + pwsh (una sola vez, setup inicial)
powershell
# En el servidor Windows
Add-WindowsCapability -Online -Name OpenSSH.Server~~~~0.0.1.0
Start-Service sshd
Set-Service -Name sshd -StartupType 'Automatic'

New-ItemProperty -Path "HKLM:\SOFTWARE\OpenSSH" -Name DefaultShell -Value "C:\Program Files\PowerShell\7\pwsh.exe" -PropertyType String -Force
Restart-Service sshd
2. Confirmar ASP.NET habilitado en IIS (si es servidor nuevo)
powershell
Install-WindowsFeature Web-Asp-Net45
3. Registrar y levantar los sitios en IIS (remoto vía SSH)
bash
ssh Administrador@192.168.50.243 "powershell -File C:\Scripts\launch-sites.ps1"

Este script (launch-sites.ps1, guardado en el servidor) crea UN sitio en el puerto 8081 con las dos
apps como aplicaciones virtuales, cada una con su App Pool + permisos ACL:

- http://192.168.50.243:8081/taxes  -> WebApp_MVC
- http://192.168.50.243:8081/legacy -> WebApp_WebForms

El gateway manda /taxes y /legacy a ese puerto sin quitar el prefijo. Las rutas físicas son parámetros
del script (-MvcPath, -WebFormsPath).

Después de publicar cada app (configuración Release, que aplica Web.Release.config), copiar su
Secrets.config a la carpeta física: no es parte del proyecto, así que el publish no lo lleva ni lo borra
(salvo que se publique con "eliminar archivos adicionales en el destino").
4. Abrir el puerto en el firewall (remoto vía SSH)
bash
ssh Administrador@192.168.50.243 "powershell -Command \"New-NetFirewallRule -DisplayName 'IIS-legadosaas-8081' -Direction Inbound -Protocol TCP -LocalPort 8081 -Action Allow\""
5. Verificar
bash
# Desde el propio servidor (descarta problema de firewall vs app)
ssh Administrador@192.168.50.243 "powershell -Command \"(Invoke-WebRequest -Uri http://localhost:8081/legacy/ -UseBasicParsing).StatusCode\""

# Desde tu máquina, directo a IIS y por el gateway
curl -I http://192.168.50.243:8081/legacy/
curl -I http://192.168.50.10/legacy/
curl -I http://192.168.50.10/taxes/Home/Menu   # 302 a Keycloak

O en el navegador: http://192.168.50.10/taxes y http://192.168.50.10/legacy (por el gateway).