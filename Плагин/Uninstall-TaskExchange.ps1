param(
    [ValidateSet('2021','2022','2023','2024')]
    [string[]]$RevitVersion = @('2021','2022','2023','2024')
)

$ErrorActionPreference = 'Stop'
$addinsRoot = [System.IO.Path]::GetFullPath(
    (Join-Path $env:APPDATA 'Autodesk\Revit\Addins'))

$runningVersions = @()
foreach ($version in $RevitVersion) {
    $expectedProcessPath = "C:\Program Files\Autodesk\Revit $version\Revit.exe"
    $isRunning = @(Get-Process Revit -ErrorAction SilentlyContinue | Where-Object {
        $_.Path -eq $expectedProcessPath
    }).Count -gt 0
    if ($isRunning) { $runningVersions += $version }
}

if ($runningVersions.Count -gt 0) {
    throw "Close Revit before removing TaskExchange. Running versions: $($runningVersions -join ', ')."
}

foreach ($version in $RevitVersion) {
    $targetRoot = [System.IO.Path]::GetFullPath((Join-Path $addinsRoot $version))
    if (-not $targetRoot.StartsWith($addinsRoot + '\', [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Unsafe Revit Addins path: $targetRoot"
    }

    foreach ($fileName in @('TaskExchange.addin', 'TaskExchange.addin.previous')) {
        $filePath = Join-Path $targetRoot $fileName
        if (Test-Path -LiteralPath $filePath) {
            Remove-Item -LiteralPath $filePath -Force
        }
    }

    if (Test-Path -LiteralPath $targetRoot) {
        $pluginDirectories = Get-ChildItem -LiteralPath $targetRoot -Directory | Where-Object {
            $_.Name -eq 'TaskExchange' -or $_.Name.StartsWith('TaskExchange_', [System.StringComparison]::OrdinalIgnoreCase)
        }
        foreach ($directory in $pluginDirectories) {
            $directoryPath = [System.IO.Path]::GetFullPath($directory.FullName)
            if (-not $directoryPath.StartsWith($targetRoot + '\TaskExchange', [System.StringComparison]::OrdinalIgnoreCase)) {
                throw "Unsafe TaskExchange path: $directoryPath"
            }
            Remove-Item -LiteralPath $directoryPath -Recurse -Force
        }
    }

    Write-Host "Revit ${version}: TaskExchange removed."
}

Write-Host 'TaskExchange uninstall completed.'
