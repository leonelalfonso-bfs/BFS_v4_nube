# Arma el MSI de la PC de porteria.
#   powershell -ExecutionPolicy Bypass -File installer\build.ps1 -Version 4.0.1
#   powershell -ExecutionPolicy Bypass -File installer\build.ps1 -Version 4.0.1 -Edicion Estandar
# La Estandar (una empresa, sin API ni nube) sale en artifacts\estandar, aparte de la Completa.
# Requisitos: .NET 10 SDK y WiX (dotnet tool install --global wix).
# (Sin acentos a proposito: Windows PowerShell 5.1 lee los .ps1 sin BOM como ANSI.)
param(
    [string]$Version = "4.0.23",
    [ValidateSet("Completa", "Estandar")]
    [string]$Edicion = "Completa"
)

$ErrorActionPreference = "Stop"
$raiz = Split-Path -Parent $PSScriptRoot
if ($Edicion -eq "Estandar") {
    $salida = Join-Path $raiz "artifacts\estandar"
    $msi = Join-Path $salida "BFS-Pesaje-Estandar-$Version.msi"
} else {
    $salida = Join-Path $raiz "artifacts"
    $msi = Join-Path $salida "BFS-Pesaje-$Version.msi"
}
$publish = Join-Path $salida "publish"
$wxs = Join-Path $PSScriptRoot "BfsPesaje.wxs"
$icono = Join-Path $raiz "src\Bfs.Desktop\bfs.ico"

if (Test-Path $publish) { Remove-Item -Recurse -Force $publish }
New-Item -ItemType Directory -Force $salida | Out-Null

Write-Host "Publicando Bfs.Desktop $Version $Edicion (self-contained win-x64)..."
# La edicion queda grabada en el programa (Bfs.Core): al cambiarla se regenera su AssemblyInfo y se recompila.
dotnet publish (Join-Path $raiz "src\Bfs.Desktop\Bfs.Desktop.csproj") `
    -c Release -r win-x64 --self-contained true `
    "-p:Version=$Version" "-p:DebugType=none" "-p:BfsEdicion=$Edicion" `
    -o $publish
if ($LASTEXITCODE -ne 0) { throw "Fallo dotnet publish" }

# La configuracion de desarrollo (licencia desactivada) nunca va al instalador.
Get-ChildItem $publish -Filter "appsettings.Development.json" -Recurse | Remove-Item -Force

Write-Host "Armando $msi..."
wix build $wxs -arch x64 -d "Version=$Version" -d "Edicion=$Edicion" -d "PublishDir=$publish" -d "Icono=$icono" -o $msi
if ($LASTEXITCODE -ne 0) { throw "Fallo wix build" }

$tam = [math]::Round((Get-Item $msi).Length / 1MB, 1)
Write-Host "Listo: $msi ($tam MB)"
