@echo off
setlocal enabledelayedexpansion

:: 1. FIX: Search for files that have IL2Cpp in the middle, not just at the end
for %%f in (*_IL2Cpp_*.dll) do (
    set "DLL_NAME=%%~nf"
    
    :: 2. FIX: Extract version by taking everything AFTER "_IL2Cpp_"
    :: Logic: Replaces everything up to "_IL2Cpp_" with an empty string
    set "DLL_VERSION=!DLL_NAME:*_IL2Cpp_=!"
    
    :: 3. FIX: Create a clean BASE_NAME by removing "_IL2Cpp_" and the version
    :: This requires a small trick to use the version variable we just found
    call set "BASE_NAME=%%DLL_NAME:_IL2Cpp_!DLL_VERSION!=%%"
    
    goto :check_manifest
)

:check_manifest
if not defined DLL_VERSION (
    echo Error: Could not extract version from DLL filename.
    echo Ensure your file is named like "ModName_IL2Cpp_1.0.0.dll"
    pause
    exit /b 1
)

:: Use Node.js to read version from manifest.json
echo Verifying manifest.json version...
for /f %%i in ('node -pe "require('./manifest.json').version_number"') do set "MANIFEST_VERSION=%%i"

if "%DLL_VERSION%" neq "%MANIFEST_VERSION%" (
    echo ERROR: Version mismatch!
    echo    DLL Version:      %DLL_VERSION%
    echo    Manifest Version: %MANIFEST_VERSION%
    echo Please update manifest.json to match the DLL version.
    pause
    exit /b 1
)

echo Version check passed: %DLL_VERSION%
echo Base Name identified: %BASE_NAME%

:: Proceed with ZIP creation
echo Creating ZIP files for version: %DLL_VERSION%...

:: 1. Create the "TS" zip (all files except this script, ZIPs, and DLLs)
::    Note: Added *.dll to exclude list so raw DLLs aren't in the main TS zip
powershell -command "$filesToZip = Get-ChildItem -Exclude '%~nx0','*.zip','*.dll'; Compress-Archive -Path $filesToZip -DestinationPath '%BASE_NAME%_TS.zip' -Force"

:: 2. Create IL2Cpp and Mono zips
::    FIX: Updated Source Path to include the version number in the filename
powershell -command "Compress-Archive -Path '%BASE_NAME%_IL2Cpp_%DLL_VERSION%.dll' -DestinationPath '%BASE_NAME%_IL2Cpp.zip' -Force"
powershell -command "Compress-Archive -Path '%BASE_NAME%_Mono_%DLL_VERSION%.dll' -DestinationPath '%BASE_NAME%_Mono.zip' -Force"

echo Done.
pause