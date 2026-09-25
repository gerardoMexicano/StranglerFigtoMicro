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

Este script (launch-sites.ps1, guardado en el servidor) crea App Pool + Sitio + permisos ACL + arranque, por cada app:

powershell
New-WebAppPool -Name $poolName
Set-ItemProperty "IIS:\AppPools\$poolName" -Name "managedRuntimeVersion" -Value "v4.0"
Set-ItemProperty "IIS:\AppPools\$poolName" -Name "managedPipelineMode" -Value "Integrated"
New-Website -Name $SiteName -PhysicalPath $PhysicalPath -ApplicationPool $poolName -Port $Port
Start-WebAppPool -Name $poolName
Start-Website -Name $SiteName
4. Abrir los puertos en el firewall (remoto vía SSH)
bash
ssh Administrador@192.168.50.243 "powershell -Command \"New-NetFirewallRule -DisplayName 'IIS-legadosaas-8081' -Direction Inbound -Protocol TCP -LocalPort 8081 -Action Allow\""

ssh Administrador@192.168.50.243 "powershell -Command \"New-NetFirewallRule -DisplayName 'IIS-brooklynsaas-8082' -Direction Inbound -Protocol TCP -LocalPort 8082 -Action Allow\""
5. Verificar
bash
# Desde el propio servidor (descarta problema de firewall vs app)
ssh Administrador@192.168.50.243 "powershell -Command \"(Invoke-WebRequest -Uri http://localhost:8081 -UseBasicParsing).StatusCode\""

# Desde tu máquina
curl -I http://192.168.50.243:8081
curl -I http://192.168.50.243:8082

O directo en el navegador: http://192.168.50.243:8081 y http://192.168.50.243:8082