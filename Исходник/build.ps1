param(
    [ValidateSet('Debug','Release')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$project = Join-Path $root 'src\TaskExchange\TaskExchange.csproj'
$workspace = Split-Path -Parent $root
$dist = Join-Path $workspace 'Плагин'
$versions = @('2021','2022','2023','2024')
$env:DOTNET_CLI_HOME = Join-Path $root '.dotnet-cli'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'

foreach ($version in $versions) {
    $api = "C:\Program Files\Autodesk\Revit $version\RevitAPI.dll"
    if (-not (Test-Path -LiteralPath $api)) {
        throw "Revit API $version не найден: $api"
    }

    dotnet build $project -c $Configuration -p:RevitVersion=$version
    if ($LASTEXITCODE -ne 0) { throw "Сборка Revit $version завершилась с ошибкой." }

    $package = Join-Path $dist "Revit$version"
    $plugin = Join-Path $package 'TaskExchange'
    New-Item -ItemType Directory -Force -Path $plugin | Out-Null
    Copy-Item -LiteralPath (Join-Path $root "artifacts\Revit$version\TaskExchange\TaskExchange.dll") -Destination $plugin -Force
    Copy-Item -LiteralPath (Join-Path $root "artifacts\Revit$version\TaskExchange\TaskExchange.pdb") -Destination $plugin -Force
    Copy-Item -LiteralPath (Join-Path $root 'resources\UNI_Задание.rfa') -Destination $plugin -Force

    $assemblyPath = 'TaskExchange\TaskExchange.dll'
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
    Set-Content -LiteralPath (Join-Path $package 'TaskExchange.addin') -Value $manifest -Encoding UTF8
    Set-Content -LiteralPath (Join-Path $package 'version.txt') -Value $version -Encoding ASCII
    Copy-Item -LiteralPath (Join-Path $root 'package-install.ps1') -Destination (Join-Path $package 'install.ps1') -Force
}

foreach ($version in $versions) {
    $archive = Join-Path $dist "TaskExchange-Revit$version.zip"
    Compress-Archive -Path (Join-Path $dist "Revit$version\*") -DestinationPath $archive -Force
}

Write-Host "Готово: $dist"
