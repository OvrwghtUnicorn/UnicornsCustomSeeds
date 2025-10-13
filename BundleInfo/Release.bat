@echo off
setlocal enabledelayedexpansion

:: Extract version from DLL (e.g., "HireMe_1.3.0" -> "1.3.0")
for %%f in (*_IL2Cpp.dll) do (
    set "DLL_NAME=%%~nf"
    set "BASE_NAME=!DLL_NAME:_IL2Cpp=!"
    for /f "tokens=2 delims=_" %%v in ("!DLL_NAME!") do set "DLL_VERSION=%%v"
    goto :check_manifest
)

:check_manifest
if not defined DLL_VERSION (
    echo Error: Could not extract version from DLL filename.
    pause
    exit /b 1
)

:: Use Node.js to read version from manifest.json
echo Verifying manifest.json version...
for /f %%i in ('node -pe "require('./manifest.json').version_number"') do set "MANIFEST_VERSION=%%i"

if "%DLL_VERSION%" neq "%MANIFEST_VERSION%" (
    echo ERROR: Version mismatch!
    echo   DLL Version:    %DLL_VERSION%
    echo   Manifest Version: %MANIFEST_VERSION%
    echo Please update manifest.json to match the DLL version.
    pause
    exit /b 1
)

echo Version check passed: %DLL_VERSION%

:: Proceed with ZIP creation
echo Creating ZIP files for version: %BASE_NAME%...

:: 1. Create the "TS" zip (all files except this script and other ZIPs)
powershell -command "$filesToZip = Get-ChildItem -Exclude '%~nx0','*.zip'; Compress-Archive -Path $filesToZip -DestinationPath '%BASE_NAME%_TS.zip'"

:: 2. Create IL2Cpp and Mono zips (single DLL each)
powershell -command "Compress-Archive -Path '%BASE_NAME%_IL2Cpp.dll' -DestinationPath '%BASE_NAME%_IL2Cpp.zip'"
powershell -command "Compress-Archive -Path '%BASE_NAME%_Mono.dll' -DestinationPath '%BASE_NAME%_Mono.zip'"

echo Done.
pause