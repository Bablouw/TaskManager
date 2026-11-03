@echo off
setlocal
chcp 65001 >nul

set "INSTALL_SCRIPT="
for %%F in ("%~dp0*.ps1") do (
    if /I not "%%~nxF"=="Uninstall-TaskExchange.ps1" (
        if defined INSTALL_SCRIPT goto multiple_scripts
        set "INSTALL_SCRIPT=%%~fF"
    )
)

if not defined INSTALL_SCRIPT goto script_not_found

echo Installing TaskExchange...
set "TASKEXCHANGE_PACKAGE_ROOT=%~dp0"
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -Command "$source = Get-Content -LiteralPath '%INSTALL_SCRIPT%' -Raw -Encoding UTF8; & ([ScriptBlock]::Create($source)) %*"
set "INSTALL_EXIT_CODE=%ERRORLEVEL%"

if not "%INSTALL_EXIT_CODE%"=="0" (
    echo.
    echo Installation failed with exit code %INSTALL_EXIT_CODE%.
) else (
    echo.
    echo TaskExchange installation completed successfully.
)
goto finish

:multiple_scripts
echo More than one PowerShell installer was found in:
echo %~dp0
set "INSTALL_EXIT_CODE=2"
goto finish

:script_not_found
echo PowerShell installer was not found in:
echo %~dp0
set "INSTALL_EXIT_CODE=2"

:finish
if not defined TASKEXCHANGE_NO_PAUSE pause
exit /b %INSTALL_EXIT_CODE%
