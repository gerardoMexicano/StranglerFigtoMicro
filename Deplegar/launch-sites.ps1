# Un sitio de IIS en un solo puerto con las dos apps legacy como aplicaciones virtuales:
#   http://<servidor>:8081/taxes   -> WebApp_MVC
#   http://<servidor>:8081/legacy  -> WebApp_WebForms
# El gateway (07_reverse/01_deployment) manda /taxes y /legacy a este puerto SIN quitar el prefijo,
# así cada app sabe que vive bajo su ruta (~/, Url.Action, /signin-oidc) y no hay que reescribir URLs.
# Ajustar las rutas físicas a las del servidor.
param(
    [string]$SiteName     = "legadosaas",
    [string]$SitePath     = "C:\Deploy\legadosaas",
    [int]   $Port         = 8081,

    [string]$MvcPath      = "C:\Deploy\taxes",
    [string]$WebFormsPath = "C:\Deploy\legacy"
)

Import-Module WebAdministration
$ErrorActionPreference = "Stop"

function New-DotNetPool {
    param($PoolName)

    if (-not (Test-Path "IIS:\AppPools\$PoolName")) {
        New-WebAppPool -Name $PoolName | Out-Null
    }
    Set-ItemProperty "IIS:\AppPools\$PoolName" -Name "managedRuntimeVersion" -Value "v4.0"
    Set-ItemProperty "IIS:\AppPools\$PoolName" -Name "managedPipelineMode" -Value "Integrated"
    Set-ItemProperty "IIS:\AppPools\$PoolName" -Name "processModel.identityType" -Value "ApplicationPoolIdentity"
}

function Grant-PoolRead {
    param($PoolName, $PhysicalPath)

    # Dar permisos de lectura al identity del pool sobre la carpeta
    $acl = Get-Acl $PhysicalPath
    $rule = New-Object System.Security.AccessControl.FileSystemAccessRule(
        "IIS AppPool\$PoolName", "ReadAndExecute", "ContainerInherit,ObjectInherit", "None", "Allow"
    )
    $acl.AddAccessRule($rule)
    Set-Acl -Path $PhysicalPath -AclObject $acl
}

function New-DotNetApp {
    param($AppName, $PhysicalPath)

    # Un pool por app: se reciclan por separado y cada una conserva su machineKey / sesión
    $poolName = "$SiteName-$AppName-Pool"
    Write-Host "== /$AppName -> $PhysicalPath ==" -ForegroundColor Cyan

    New-Item -ItemType Directory -Force -Path $PhysicalPath | Out-Null
    New-DotNetPool -PoolName $poolName

    if (-not (Get-WebApplication -Site $SiteName -Name $AppName)) {
        New-WebApplication -Site $SiteName -Name $AppName -PhysicalPath $PhysicalPath -ApplicationPool $poolName | Out-Null
    } else {
        Set-ItemProperty "IIS:\Sites\$SiteName\$AppName" -Name physicalPath -Value $PhysicalPath
        Set-ItemProperty "IIS:\Sites\$SiteName\$AppName" -Name applicationPool -Value $poolName
    }

    Grant-PoolRead -PoolName $poolName -PhysicalPath $PhysicalPath
    Start-WebAppPool -Name $poolName -ErrorAction SilentlyContinue
}

# Sitio contenedor: su raíz no sirve ninguna app, solo aloja /taxes y /legacy
Write-Host "== Sitio $SiteName en puerto $Port ==" -ForegroundColor Cyan
New-Item -ItemType Directory -Force -Path $SitePath | Out-Null
New-DotNetPool -PoolName "$SiteName-Pool"
if (-not (Test-Path "IIS:\Sites\$SiteName")) {
    New-Website -Name $SiteName -PhysicalPath $SitePath -ApplicationPool "$SiteName-Pool" -Port $Port | Out-Null
} else {
    Set-ItemProperty "IIS:\Sites\$SiteName" -Name physicalPath -Value $SitePath
}
Grant-PoolRead -PoolName "$SiteName-Pool" -PhysicalPath $SitePath

New-DotNetApp -AppName "taxes"  -PhysicalPath $MvcPath
New-DotNetApp -AppName "legacy" -PhysicalPath $WebFormsPath

Start-WebAppPool -Name "$SiteName-Pool" -ErrorAction SilentlyContinue
Start-Website -Name $SiteName

Write-Host "`n== Estado final ==" -ForegroundColor Cyan
Get-WebApplication -Site $SiteName | Format-Table Path, ApplicationPool, PhysicalPath
