<#
.SYNOPSIS
    Script de inicializacion (Bootstrap) para servidores de despliegue en Windows.
.DESCRIPTION
    Este script automatiza la instalacion y configuracion inicial de un servidor web IIS,
    herramientas esenciales (.NET Hosting Bundle, Git, URL Rewrite, Web Deploy, OpenSSH Server),
    usuarios de servicio, directorios de despliegue con permisos correctos, firewall y HTTPS.
    Debe ejecutarse una sola vez con privilegios de Administrador.
.PARAMETER DeployPath
    Ruta donde se alojaran los despliegues de aplicaciones web. Por defecto 'C:\Deploy'.
.PARAMETER ServiceUserName
    Nombre del usuario local del sistema que ejecutara los servicios y pools de IIS. Por defecto 'SvcApp'.
.PARAMETER ServiceUserPassword
    Contrasena en texto plano para el usuario de servicio. Si no se provee, se generara una aleatoria.
.PARAMETER InstallSqlServer
    Switch para descargar e instalar SQL Server 2022 Express de forma silenciosa.
.PARAMETER DotNetVersion
    Version del .NET Hosting Bundle a descargar (por ejemplo, '8.0' o '9.0'). Por defecto '8.0'.
.PARAMETER EnableHttps
    Switch para generar un certificado autofirmado local y configurar la vinculacion HTTPS en el sitio web por defecto de IIS.
.PARAMETER SkipDotNetHosting
    Bypass para la instalacion del .NET Hosting Bundle.
.PARAMETER SkipGit
    Bypass para la instalacion de Git.
.PARAMETER SkipUrlRewrite
    Bypass para la instalacion de IIS URL Rewrite Module.
.PARAMETER SkipWebDeploy
    Bypass para la instalacion de Web Deploy.
.PARAMETER SkipOpenSsh
    Bypass para la instalacion de OpenSSH Server.
.EXAMPLE
    PowerShell.exe -ExecutionPolicy Bypass -File .\bootstrap.ps1 -InstallSqlServer -EnableHttps
#>
[CmdletBinding()]
param(
    [string]$DeployPath = "C:\Deploy",
    [string]$ServiceUserName = "SvcApp",
    [string]$ServiceUserPassword,
    [switch]$InstallSqlServer,
    [string]$DotNetVersion = "8.0",
    [switch]$EnableHttps,
    [switch]$SkipDotNetHosting,
    [switch]$SkipGit,
    [switch]$SkipUrlRewrite,
    [switch]$SkipWebDeploy,
    [switch]$SkipOpenSsh
)

# Evitar dialogos interactivos y acelerar descargas en Invoke-WebRequest
$ProgressPreference = 'SilentlyContinue'
$ErrorActionPreference = 'Stop'

Write-Host "==========================================================" -ForegroundColor Green
Write-Host "    INICIANDO CONFIGURACION BOOTSTRAP DE WINDOWS SERVER   " -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Green

# 1. Validar Privilegios de Administrador
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Error "Este script requiere privilegios de Administrador. Por favor, abre PowerShell como Administrador y vuelve a intentarlo."
    Exit 1
}

# 2. Helper de Descarga e Instalacion Silenciosa
function Install-Software {
    param (
        [string]$Name,
        [string]$Url,
        [string]$FileName,
        [string]$Arguments,
        [bool]$IsMsi = $false
    )
    
    Write-Host "`n[+] Instalando $($Name)..." -ForegroundColor Cyan
    $tempDir = Join-Path $env:TEMP "BootstrapInstallers"
    if (-not (Test-Path $tempDir)) {
        New-Item -ItemType Directory -Path $tempDir -Force | Out-Null
    }
    $filePath = Join-Path $tempDir $FileName
    
    Write-Host "  -> Descargando desde: $Url" -ForegroundColor Gray
    try {
        Invoke-WebRequest -Uri $Url -OutFile $filePath -UseBasicParsing
        Write-Host "  -> Descarga completada con exito." -ForegroundColor Gray
    } catch {
        Write-Warning "  [!] Error al descargar $($Name): $_"
        return
    }
    
    Write-Host "  -> Ejecutando instalador en segundo plano..." -ForegroundColor Gray
    try {
        if ($IsMsi) {
            $procArgs = "/i `"$filePath`" $Arguments"
            $proc = Start-Process -FilePath "msiexec.exe" -ArgumentList $procArgs -Wait -PassThru -NoNewWindow
        } else {
            $proc = Start-Process -FilePath $filePath -ArgumentList $Arguments -Wait -PassThru -NoNewWindow
        }
        
        # 0 = Exito, 3010 = Exito con necesidad de reinicio posterior
        if ($proc.ExitCode -eq 0 -or $proc.ExitCode -eq 3010) {
            Write-Host "  [OK] $($Name) se instalo correctamente (ExitCode: $($proc.ExitCode))" -ForegroundColor Green
        } else {
            Write-Warning "  [!] $($Name) finalizo con un codigo de salida inesperado: $($proc.ExitCode)"
        }
    } catch {
        Write-Warning "  [!] Error durante la instalacion de $($Name): $_"
    } finally {
        if (Test-Path $filePath) {
            Remove-Item -Path $filePath -Force -ErrorAction SilentlyContinue
        }
    }
}

# 3. Habilitar Caracteristicas de IIS
Write-Host "`n[+] Configurando caracteristicas de IIS..." -ForegroundColor Cyan
$os = Get-CimInstance Win32_OperatingSystem
if ($os.ProductType -eq 1) {
    Write-Host "  -> Detectado Windows Client (Escritorio). Usando Enable-WindowsOptionalFeature..." -ForegroundColor Gray
    $features = @(
        "IIS-WebServerRole", "IIS-WebServer", "IIS-CommonHttpFeatures", 
        "IIS-StaticContent", "IIS-DefaultDocument", "IIS-DirectoryBrowsing", 
        "IIS-HttpErrors", "IIS-ApplicationDevelopment", "IIS-ASPNET45", 
        "IIS-NetFxExtensibility45", "IIS-ISAPIExtensions", "IIS-ISAPIFilter", 
        "IIS-HealthAndDiagnostics", "IIS-HttpLogging", "IIS-RequestMonitor", 
        "IIS-Security", "IIS-BasicAuthentication", "IIS-WindowsAuthentication", 
        "IIS-RequestFiltering", "IIS-WebServerManagementTools", "IIS-ManagementConsole"
    )
    foreach ($feature in $features) {
        try {
            Enable-WindowsOptionalFeature -Online -FeatureName $feature -All -NoRestart -ErrorAction SilentlyContinue | Out-Null
            Write-Host "    - Habilitada: $feature" -ForegroundColor Gray
        } catch {
            Write-Warning "    [!] Fallo al habilitar feature: $feature"
        }
    }
} else {
    Write-Host "  -> Detectado Windows Server. Usando Install-WindowsFeature..." -ForegroundColor Gray
    try {
        Import-Module ServerManager
        $features = @(
            "Web-Server", "Web-Mgmt-Console", "Web-Common-Http", "Web-Static-Content", 
            "Web-Default-Doc", "Web-Dir-Browsing", "Web-Http-Errors", "Web-Http-Logging", 
            "Web-Request-Monitor", "Web-Security", "Web-Filtering", "Web-Basic-Auth", 
            "Web-Windows-Auth", "Web-Net-Ext45", "Web-Asp-Net45", "Web-ISAPI-Ext", "Web-ISAPI-Filter"
        )
        foreach ($feature in $features) {
            Install-WindowsFeature -Name $feature -IncludeManagementTools -ErrorAction SilentlyContinue | Out-Null
            Write-Host "    - Instalado: $feature" -ForegroundColor Gray
        }
    } catch {
        Write-Warning "  [!] Error al importar o instalar features con ServerManager: $_"
    }
}
Write-Host "  [OK] IIS habilitado y listo." -ForegroundColor Green

# 4. Instalaciones de Software de Terceros / Frameworks
if (-not $SkipDotNetHosting) {
    $dotnetUrl = "https://aka.ms/dotnet/$DotNetVersion/aspnetcore-runtime-hosting-bundle-win.exe"
    Install-Software -Name ".NET Hosting Bundle ($DotNetVersion)" -Url $dotnetUrl -FileName "dotnet-hosting.exe" -Arguments "/quiet /norestart"
}

if (-not $SkipGit) {
    $gitUrl = "https://github.com/git-for-windows/git/releases/latest/download/Git-64-bit.exe"
    # Argumentos para instalacion super silenciosa por defecto
    $gitArgs = "/VERYSILENT /NORESTART /NOCANCEL /SP- /CLOSEAPPLICATIONS /RESTARTAPPLICATIONS"
    Install-Software -Name "Git" -Url $gitUrl -FileName "git-install.exe" -Arguments $gitArgs
}

if (-not $SkipUrlRewrite) {
    $rewriteUrl = "https://download.microsoft.com/download/1/2/8/128E2E22-C1B9-44A8-A07F-7A69085F0245/rewrite_amd64_en-US.msi"
    Install-Software -Name "IIS URL Rewrite Module 2.1" -Url $rewriteUrl -FileName "rewrite.msi" -Arguments "/qn /norestart" -IsMsi $true
}

if (-not $SkipWebDeploy) {
    $webDeployUrl = "https://download.microsoft.com/download/0/1/D/01D2E997-4B52-47C0-B955-4FE1A6572F8B/WebDeploy_amd64_en-US.msi"
    # ADDLOCAL=ALL instala todos los modulos, incluyendo el servicio de agente para despliegues remotos
    Install-Software -Name "Microsoft Web Deploy 3.6" -Url $webDeployUrl -FileName "webdeploy.msi" -Arguments "/qn /norestart ADDLOCAL=ALL" -IsMsi $true
}

if ($InstallSqlServer) {
    # Descarga e instala SQL Server 2022 Express de forma desatendida
    $sqlUrl = "https://go.microsoft.com/fwlink/p/?LinkID=2215158"
    $sqlArgs = "/ACTION=Install /FEATURES=SQL /INSTANCENAME=SQLEXPRESS /SQLSVCACCOUNT=""NT AUTHORITY\NetworkService"" /SQLSYSADMINACCOUNTS=""BUILTIN\Administrators"" /TCPENABLED=1 /IAcceptSQLServerLicenseTerms /Q"
    Install-Software -Name "SQL Server 2022 Express" -Url $sqlUrl -FileName "SQL2022-SSEI-Expr.exe" -Arguments $sqlArgs
}

# 5. Configurar OpenSSH Server
if (-not $SkipOpenSsh) {
    Write-Host "`n[+] Configurando OpenSSH Server..." -ForegroundColor Cyan
    $sshService = Get-Service -Name "sshd" -ErrorAction SilentlyContinue
    if (-not $sshService) {
        try {
            Write-Host "  -> Agregando Capability OpenSSH.Server..." -ForegroundColor Gray
            Add-WindowsCapability -Online -Name OpenSSH.Server~~~~0.0.1.0 -ErrorAction Stop | Out-Null
            Write-Host "  [OK] OpenSSH Server instalado correctamente." -ForegroundColor Green
        } catch {
            Write-Warning "  [!] No se pudo instalar OpenSSH Server de forma nativa: $_"
        }
    } else {
        Write-Host "  -> OpenSSH Server ya esta instalado." -ForegroundColor Gray
    }
    
    # Iniciar y habilitar servicio sshd
    try {
        Set-Service -Name "sshd" -StartupType Automatic
        Start-Service -Name "sshd"
        Write-Host "  [OK] Servicio sshd configurado e iniciado." -ForegroundColor Green
    } catch {
        Write-Warning "  [!] Error al iniciar/configurar el servicio sshd: $_"
    }
}

# 6. Crear Carpetas de Despliegue
Write-Host "`n[+] Creando directorios..." -ForegroundColor Cyan
if (-not (Test-Path $DeployPath)) {
    New-Item -ItemType Directory -Path $DeployPath -Force | Out-Null
    Write-Host "  [OK] Directorio de despliegue creado: $DeployPath" -ForegroundColor Green
} else {
    Write-Host "  -> El directorio $DeployPath ya existia." -ForegroundColor Gray
}

# 7. Crear Usuario de Servicio y Permisos ACL
Write-Host "`n[+] Configurando usuario de servicio..." -ForegroundColor Cyan
if (-not $ServiceUserPassword) {
    $chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!@#$%^&*"
    $randomPassword = ""
    for ($i = 0; $i -lt 16; $i++) {
        $randomPassword += $chars[(Get-Random -Minimum 0 -Maximum $chars.Length)]
    }
    $ServiceUserPassword = $randomPassword
    Write-Host "  -> [AVISO] Contrasena generada automaticamente para $($ServiceUserName): $($ServiceUserPassword)" -ForegroundColor Yellow
}

$userExists = Get-LocalUser -Name $ServiceUserName -ErrorAction SilentlyContinue
if (-not $userExists) {
    try {
        $securePassword = ConvertTo-SecureString $ServiceUserPassword -AsPlainText -Force
        New-LocalUser -Name $ServiceUserName -Password $securePassword -FullName "Usuario de Servicio de Aplicaciones" -Description "Creado automaticamente por bootstrap.ps1" -PasswordNeverExpires $true -UserMayNotChangePassword $true | Out-Null
        Write-Host "  [OK] Usuario de servicio '$ServiceUserName' creado." -ForegroundColor Green
        
        $groupExists = Get-LocalGroup -Name "IIS_IUSRS" -ErrorAction SilentlyContinue
        if ($groupExists) {
            Add-LocalGroupMember -Group "IIS_IUSRS" -Member $ServiceUserName -ErrorAction SilentlyContinue
            Write-Host "  -> Usuario anadido al grupo local 'IIS_IUSRS'." -ForegroundColor Gray
        }
    } catch {
        Write-Warning "  [!] Error al crear el usuario local o anadirlo a IIS_IUSRS: $_"
    }
} else {
    Write-Host "  -> El usuario de servicio '$ServiceUserName' ya existe." -ForegroundColor Yellow
}

# Configuracion de permisos ACL en el directorio de despliegue
Write-Host "  -> Configurando permisos de directorio para $ServiceUserName e IIS..." -ForegroundColor Gray
try {
    $acl = Get-Acl -Path $DeployPath
    
    # Otorgar Control de Modificacion (Modify) al usuario de servicio
    $arServiceUser = New-Object System.Security.AccessControl.FileSystemAccessRule($ServiceUserName, "Modify", "ContainerInherit, ObjectInherit", "None", "Allow")
    $acl.SetAccessRule($arServiceUser)
    
    # Otorgar permisos de lectura y ejecucion a IIS_IUSRS
    $arIisUsers = New-Object System.Security.AccessControl.FileSystemAccessRule("IIS_IUSRS", "ReadAndExecute", "ContainerInherit, ObjectInherit", "None", "Allow")
    $acl.SetAccessRule($arIisUsers)
    
    Set-Acl -Path $DeployPath -AclObject $acl
    Write-Host "  [OK] Permisos ACL aplicados correctamente a $($DeployPath)." -ForegroundColor Green
} catch {
    Write-Warning "  [!] Fallo la aplicacion de permisos ACL en $($DeployPath): $_"
}

# 8. Configurar Firewall de Windows
Write-Host "`n[+] Configurando Firewall de Windows..." -ForegroundColor Cyan
$firewallRules = @(
    @{ Name = "IIS-HTTP-In"; DisplayName = "IIS HTTP (Puerto 80)"; Port = 80; Protocol = "TCP" },
    @{ Name = "IIS-HTTPS-In"; DisplayName = "IIS HTTPS (Puerto 443)"; Port = 443; Protocol = "TCP" },
    @{ Name = "OpenSSH-sshd-In"; DisplayName = "OpenSSH Server (Puerto 22)"; Port = 22; Protocol = "TCP" }
)

if ($InstallSqlServer) {
    $firewallRules += @{ Name = "SQLServer-1433-In"; DisplayName = "SQL Server Database Engine (Puerto 1433)"; Port = 1433; Protocol = "TCP" }
}

foreach ($rule in $firewallRules) {
    $existing = Get-NetFirewallRule -Name $rule.Name -ErrorAction SilentlyContinue
    if (-not $existing) {
        try {
            New-NetFirewallRule -Name $rule.Name -DisplayName $rule.DisplayName -Enabled True -Direction Inbound -Protocol $rule.Protocol -LocalPort $rule.Port -Action Allow | Out-Null
            Write-Host "  [OK] Regla del Firewall creada: $($rule.DisplayName)" -ForegroundColor Green
        } catch {
            Write-Warning "  [!] Error al crear la regla de firewall $($rule.DisplayName): $_"
        }
    } else {
        Write-Host "  -> Regla de Firewall ya existe: $($rule.DisplayName)" -ForegroundColor Gray
    }
}

# 9. Configuracion de Certificados Autofirmados y Enlaces HTTPS en IIS
if ($EnableHttps) {
    Write-Host "`n[+] Configurando Certificado SSL Autofirmado..." -ForegroundColor Cyan
    try {
        $hostname = $env:COMPUTERNAME
        # Buscar si ya existe un certificado autofirmado valido para evitar redundancia
        $cert = Get-ChildItem -Path "Cert:\LocalMachine\My" | Where-Object { $_.Subject -like "*CN=$hostname*" -or $_.Subject -like "*CN=localhost*" } | Select-Object -First 1
        
        if (-not $cert) {
            Write-Host "  -> No se encontro un certificado coincidente. Generando nuevo certificado autofirmado..." -ForegroundColor Gray
            $cert = New-SelfSignedCertificate -DnsName $hostname, "localhost" -CertStoreLocation "cert:\LocalMachine\My" -FriendlyName "Bootstrap Self-Signed Certificate"
            Write-Host "  [OK] Certificado generado con Huella (Thumbprint): $($cert.Thumbprint)" -ForegroundColor Green
        } else {
            Write-Host "  -> Reutilizando certificado existente con Huella (Thumbprint): $($cert.Thumbprint)" -ForegroundColor Yellow
        }
        
        # Vincular el certificado a IIS
        Import-Module WebAdministration -ErrorAction SilentlyContinue
        if (Get-Website -Name "Default Web Site" -ErrorAction SilentlyContinue) {
            # Comprobar si ya existe una vinculacion HTTPS en el puerto 443
            $binding = Get-WebBinding -Name "Default Web Site" -IPAddress "*" -Port 443 -Protocol "https" -ErrorAction SilentlyContinue
            if (-not $binding) {
                New-WebBinding -Name "Default Web Site" -IPAddress "*" -Port 443 -Protocol "https" -ErrorAction SilentlyContinue
                Write-Host "  -> Enlace HTTPS (puerto 443) agregado a 'Default Web Site'." -ForegroundColor Gray
            }
            
            # Asociar el certificado SSL
            if (Test-Path "IIS:\SslBindings") {
                $bindingPath = "IIS:\SslBindings\0.0.0.0!443"
                if (Test-Path $bindingPath) {
                    Remove-Item $bindingPath -Force
                }
                New-Item -Path $bindingPath -Value $cert -Force | Out-Null
                Write-Host "  [OK] Certificado vinculado correctamente al sitio web predeterminado en IIS." -ForegroundColor Green
            } else {
                # Alternativa via netsh si el proveedor de IIS no esta cargado completamente en la sesion
                $guid = [Guid]::NewGuid().ToString("B")
                netsh http delete sslcert ipport=0.0.0.0:443 2>$null | Out-Null
                netsh http add sslcert ipport=0.0.0.0:443 certhash=$($cert.Thumbprint) appid="$guid" | Out-Null
                Write-Host "  [OK] Certificado vinculado correctamente via netsh." -ForegroundColor Green
            }
        } else {
            Write-Warning "  [!] El sitio 'Default Web Site' de IIS no fue encontrado. No se configuro HTTPS."
        }
    } catch {
        Write-Warning "  [!] Error al configurar certificados SSL o enlaces de IIS: $_"
    }
}

Write-Host "`n==========================================================" -ForegroundColor Green
Write-Host "  PROCESO DE BOOTSTRAP COMPLETADO CON EXITO                " -ForegroundColor Green
if ($randomPassword) {
    Write-Host "  IMPORTANTE: Guarda la contrasena del usuario '$($ServiceUserName)':" -ForegroundColor Red
    Write-Host "  --> $($randomPassword)" -ForegroundColor Yellow
}
Write-Host "==========================================================" -ForegroundColor Green
