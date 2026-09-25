param(
    [string]$Site1Name = "legadosaas",
    [string]$Site1Path = "C:\Deploy\legadosaas",
    [int]   $Site1Port = 8081,

    [string]$Site2Name = "brooklynsaas",
    [string]$Site2Path = "C:\Deploy\brooklynsaas",
    [int]   $Site2Port = 8082
)

Import-Module WebAdministration
$ErrorActionPreference = "Stop"

function New-DotNetSite {
    param($SiteName, $PhysicalPath, $Port)

    $poolName = "$SiteName-Pool"

    Write-Host "== Configurando $SiteName ==" -ForegroundColor Cyan

    # Crear App Pool si no existe
    if (-not (Test-Path "IIS:\AppPools\$poolName")) {
        New-WebAppPool -Name $poolName
    }
    Set-ItemProperty "IIS:\AppPools\$poolName" -Name "managedRuntimeVersion" -Value "v4.0"
    Set-ItemProperty "IIS:\AppPools\$poolName" -Name "managedPipelineMode" -Value "Integrated"
    Set-ItemProperty "IIS:\AppPools\$poolName" -Name "processModel.identityType" -Value "ApplicationPoolIdentity"

    # Crear sitio si no existe
    if (-not (Test-Path "IIS:\Sites\$SiteName")) {
        New-Website -Name $SiteName `
                    -PhysicalPath $PhysicalPath `
                    -ApplicationPool $poolName `
                    -Port $Port
    } else {
        Set-ItemProperty "IIS:\Sites\$SiteName" -Name physicalPath -Value $PhysicalPath
        Set-ItemProperty "IIS:\Sites\$SiteName" -Name applicationPool -Value $poolName
    }

    # Dar permisos de lectura al identity del pool sobre la carpeta
    $acl = Get-Acl $PhysicalPath
    $identity = "IIS AppPool\$poolName"
    $rule = New-Object System.Security.AccessControl.FileSystemAccessRule(
        $identity, "ReadAndExecute", "ContainerInherit,ObjectInherit", "None", "Allow"
    )
    $acl.AddAccessRule($rule)
    Set-Acl -Path $PhysicalPath -AclObject $acl

    # Arrancar
    Start-WebAppPool -Name $poolName
    Start-Website -Name $SiteName

    Write-Host "$SiteName arriba en puerto $Port (Pool: $poolName)" -ForegroundColor Green
}

New-DotNetSite -SiteName $Site1Name -PhysicalPath $Site1Path -Port $Site1Port
New-DotNetSite -SiteName $Site2Name -PhysicalPath $Site2Path -Port $Site2Port

Write-Host "`n== Estado final ==" -ForegroundColor Cyan
Get-Website | Where-Object { $_.Name -in @($Site1Name, $Site2Name) } | Format-Table Name, State, PhysicalPath