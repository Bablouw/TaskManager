@echo off
setlocal
chcp 65001 >nul

set "UNINSTALL_SCRIPT=%~dp0Uninstall-TaskExchange.ps1"
if not exist "%UNINSTALL_SCRIPT%" goto script_not_found

echo Removing TaskExchange from Revit...
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%UNINSTALL_SCRIPT%" %*
set "UNINSTALL_EXIT_CODE=%ERRORLEVEL%"

if not "%UNINSTALL_EXIT_CODE%"=="0" (
    echo.
    echo Uninstall failed with exit code %UNINSTALL_EXIT_CODE%.
) else (
    echo.
    echo TaskExchange was removed from Revit successfully.
)
goto finish

:script_not_found
echo Uninstaller was not found:
echo %UNINSTALL_SCRIPT%
set "UNINSTALL_EXIT_CODE=2"

:finish
if not defined TASKEXCHANGE_NO_PAUSE pause
exit /b %UNINSTALL_EXIT_CODE%
