$ErrorActionPreference = 'Stop'
$package = Split-Path -Parent $MyInvocation.MyCommand.Path
$versionFile = Join-Path $package 'version.txt'
if (-not (Test-Path -LiteralPath $versionFile)) { throw 'Не найден version.txt в папке пакета.' }
$version = (Get-Content -Raw -LiteralPath $versionFile).Trim()
if ($version -notin @('2021','2022','2023','2024')) { throw "Неподдерживаемая версия Revit: $version" }

$sourcePlugin = Join-Path $package 'TaskExchange'
$targetRoot = Join-Path $env:APPDATA "Autodesk\Revit\Addins\$version"
$sourceDll = Join-Path $sourcePlugin 'TaskExchange.dll'
Get-ChildItem -LiteralPath $package -Recurse -File | Unblock-File -ErrorAction SilentlyContinue
$running = @(Get-Process Revit -ErrorAction SilentlyContinue | Where-Object {
    $_.Path -eq "C:\Program Files\Autodesk\Revit $version\Revit.exe"
}).Count -gt 0
$suffix = if ($running) { '_' + (Get-FileHash -Algorithm SHA256 -LiteralPath $sourceDll).Hash.Substring(0, 10) } else { '' }
$targetPlugin = Join-Path $targetRoot ('TaskExchange' + $suffix)
New-Item -ItemType Directory -Force -Path $targetRoot | Out-Null
New-Item -ItemType Directory -Force -Path $targetPlugin | Out-Null
Copy-Item -LiteralPath $sourceDll -Destination $targetPlugin -Force
Copy-Item -LiteralPath (Join-Path $sourcePlugin 'TaskExchange.pdb') -Destination $targetPlugin -Force
Copy-Item -LiteralPath (Join-Path $sourcePlugin 'UNI_Задание.rfa') -Destination $targetPlugin -Force
Get-ChildItem -LiteralPath $targetPlugin -Recurse -File | Unblock-File -ErrorAction SilentlyContinue

$assemblyPath = Join-Path $targetPlugin 'TaskExchange.dll'
$manifest = @"
<?xml version="1.0" encoding="utf-8" standalone="no"?>
<RevitAddIns>
  <AddIn Type="Application">
    <Name>Обмен заданиями</Name>
    <Assembly>$assemblyPath</Assembly>
    <AddInId>A75D7118-E799-4EAF-8EEA-02E8D4828A42</AddInId>
    <FullClassName>TaskExchange.App</FullClassName>
    <VendorId>UNI</VendorId>
    <VendorDescription>Обмен заданиями между связанными моделями</VendorDescription>
  </AddIn>
</RevitAddIns>
"@
$manifestPath = Join-Path $targetRoot 'TaskExchange.addin'
if (Test-Path -LiteralPath $manifestPath) {
    Copy-Item -LiteralPath $manifestPath -Destination (Join-Path $targetRoot 'TaskExchange.addin.previous') -Force
}
Set-Content -LiteralPath $manifestPath -Value $manifest -Encoding UTF8
Unblock-File -LiteralPath $manifestPath -ErrorAction SilentlyContinue
if ($running) {
    Write-Host "Обновление подготовлено для Revit ${version}: $targetPlugin. Перезапустите Revit для загрузки новой DLL."
} else {
    Write-Host "Плагин установлен для Revit ${version}: $targetPlugin"
}
