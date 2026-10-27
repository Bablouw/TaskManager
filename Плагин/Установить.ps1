param(
    [ValidateSet('2021','2022','2023','2024')]
    [string[]]$RevitVersion
)

$ErrorActionPreference = 'Stop'
$packageRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
if ($packageRoot.StartsWith('\\')) {
    throw 'Запустите установщик из локальной папки, а не с сетевого диска.'
}

$supported = @('2021','2022','2023','2024')
if (-not $RevitVersion -or $RevitVersion.Count -eq 0) {
    $RevitVersion = @($supported | Where-Object {
        Test-Path -LiteralPath "C:\Program Files\Autodesk\Revit $_\Revit.exe"
    })
}
if ($RevitVersion.Count -eq 0) {
    throw 'Не найдены установленные версии Revit 2021–2024. Передайте версию явно: -RevitVersion 2021'
}

foreach ($version in $RevitVersion) {
    $sourcePackage = Join-Path $packageRoot "Revit$version"
    $sourcePlugin = Join-Path $sourcePackage 'TaskExchange'
    $sourceDll = Join-Path $sourcePlugin 'TaskExchange.dll'
    if (-not (Test-Path -LiteralPath $sourceDll)) {
        throw "Пакет Revit $version повреждён: не найден $sourceDll"
    }

    Get-ChildItem -LiteralPath $sourcePackage -Recurse -File | Unblock-File -ErrorAction SilentlyContinue

    $targetRoot = Join-Path $env:APPDATA "Autodesk\Revit\Addins\$version"
    $running = @(Get-Process Revit -ErrorAction SilentlyContinue | Where-Object {
        $_.Path -eq "C:\Program Files\Autodesk\Revit $version\Revit.exe"
    }).Count -gt 0
    $suffix = if ($running) {
        '_' + (Get-FileHash -Algorithm SHA256 -LiteralPath $sourceDll).Hash.Substring(0, 10)
    } else { '' }
    $targetPlugin = Join-Path $targetRoot ('TaskExchange' + $suffix)

    New-Item -ItemType Directory -Force -Path $targetRoot | Out-Null
    New-Item -ItemType Directory -Force -Path $targetPlugin | Out-Null
    Copy-Item -LiteralPath $sourceDll -Destination $targetPlugin -Force
    $pdb = Join-Path $sourcePlugin 'TaskExchange.pdb'
    if (Test-Path -LiteralPath $pdb) { Copy-Item -LiteralPath $pdb -Destination $targetPlugin -Force }
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
        Write-Host "Revit ${version}: обновление подготовлено, перезапустите Revit."
    } else {
        Write-Host "Revit ${version}: плагин установлен."
    }
}

Write-Host 'Установка завершена.'
