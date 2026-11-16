@echo off
setlocal

where /q pwsh.exe
if errorlevel 1 (
    echo ERROR: pwsh.exe was not found on PATH. 1>&2
    exit /b 1
)

pushd "%~dp0" || exit /b 1
set "result=0"

pwsh.exe -NoLogo -NoProfile -File ".\tests\performance-smoke.ps1" -Game dcs -ConfigurationFile ".\DCS-F-100D-Components-v4.1.0.0-Complete.zip"
if errorlevel 1 set "result=1"

pwsh.exe -NoLogo -NoProfile -File ".\tests\performance-smoke.ps1" -Game elite -ConfigurationFile "%LOCALAPPDATA%\Frontier Developments\Elite Dangerous\Options\Bindings\Spaz Controls.4.2.binds"
if errorlevel 1 set "result=1"

set "documents="
for /f "delims=" %%D in ('pwsh.exe -NoLogo -NoProfile -Command "[Environment]::GetFolderPath('MyDocuments')"') do set "documents=%%D"
if defined documents (
    pwsh.exe -NoLogo -NoProfile -File ".\tests\performance-smoke.ps1" -Game squadrons -ConfigurationFile "%documents%\STAR WARS Squadrons Steam\settings\ProfileOptions_profile_synced"
    if errorlevel 1 set "result=1"
) else (
    echo ERROR: Could not resolve the Documents folder for Squadrons. 1>&2
    set "result=1"
)

pwsh.exe -NoLogo -NoProfile -File ".\tests\performance-smoke.ps1" -Game war-thunder -ConfigurationFile "D:\War Thunder\War-Thunder-Review-9cddeac88a2d4b35b304e68548a440c7\War Thunder.blk"
if errorlevel 1 set "result=1"

popd
exit /b %result%