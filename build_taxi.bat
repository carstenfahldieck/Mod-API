@echo off
setlocal EnableExtensions EnableDelayedExpansion
title TaxiMod Build

echo Compiling TaxiMod...

REM === Paths ===
set "CSC=C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe"
set "SRC=G:\SteamLibrary\steamapps\common\Cities_Skylines\Files\Mods\taxi"
set "OUT=C:\Users\carst\AppData\Local\Colossal Order\Cities_Skylines\Addons\Mods\taxi"
set "MANAGED=G:\SteamLibrary\steamapps\common\Cities_Skylines\Cities_Data\Managed"

echo CSC="%CSC%"
echo SRC=%SRC%
echo OUT=%OUT%
echo MANAGED=%MANAGED%
echo.

REM === Checks ===
if not exist "%CSC%" (
  echo ERROR: csc.exe not found: %CSC%
  pause
  exit /b 1
)

if not exist "%SRC%" (
  echo ERROR: SRC folder not found: %SRC%
  pause
  exit /b 1
)

if not exist "%OUT%" (
  echo OUT folder missing, creating: %OUT%
  mkdir "%OUT%" >nul 2>&1
)

REM === Unity/CS managed framework refs (IMPORTANT: use game/Unity assemblies, not .NET 4.0) ===
if not exist "%MANAGED%\mscorlib.dll" (
  echo ERROR: mscorlib.dll not found in: %MANAGED%
  pause
  exit /b 1
)
if not exist "%MANAGED%\System.dll" (
  echo ERROR: System.dll not found in: %MANAGED%
  pause
  exit /b 1
)
if not exist "%MANAGED%\Assembly-CSharp.dll" (
  echo ERROR: Assembly-CSharp.dll not found in: %MANAGED%
  pause
  exit /b 1
)
if not exist "%MANAGED%\ICities.dll" (
  echo ERROR: ICities.dll not found in: %MANAGED%
  pause
  exit /b 1
)
if not exist "%MANAGED%\UnityEngine.dll" (
  echo ERROR: UnityEngine.dll not found in: %MANAGED%
  pause
  exit /b 1
)

REM Optional assemblies
if not exist "%MANAGED%\System.Core.dll" (
  echo WARNING: System.Core.dll not found in: %MANAGED%
)

if not exist "%MANAGED%\ColossalManaged.dll" (
  echo WARNING: ColossalManaged.dll not found in: %MANAGED%
)

REM Optional: ColossalFramework
if exist "%MANAGED%\ColossalFramework.dll" (
  set "HAS_CF=1"
) else (
  set "HAS_CF=0"
)

REM Harmony reference (local mod folder)
set "HARMONY=%OUT%\0Harmony.dll"
if not exist "%HARMONY%" (
  echo WARNING: 0Harmony.dll not found in OUT: %HARMONY%
  echo          If your code uses Harmony, copy 0Harmony.dll to the OUT folder first.
)

echo.
echo === Running compiler ===
echo.

REM Build reference list
set "REFS=/noconfig /nostdlib+"
set "REFS=%REFS% /reference:"%MANAGED%\mscorlib.dll""
set "REFS=%REFS% /reference:"%MANAGED%\System.dll""
if exist "%MANAGED%\System.Core.dll" set "REFS=%REFS% /reference:"%MANAGED%\System.Core.dll""
set "REFS=%REFS% /reference:"%MANAGED%\Assembly-CSharp.dll""
set "REFS=%REFS% /reference:"%MANAGED%\ICities.dll""
set "REFS=%REFS% /reference:"%MANAGED%\UnityEngine.dll""
if exist "%MANAGED%\ColossalManaged.dll" set "REFS=%REFS% /reference:"%MANAGED%\ColossalManaged.dll""
if "%HAS_CF%"=="1" set "REFS=%REFS% /reference:"%MANAGED%\ColossalFramework.dll""
if exist "%HARMONY%" set "REFS=%REFS% /reference:"%HARMONY%""

REM Compile all .cs in SRC
"%CSC%" ^
 /nologo ^
 /target:library ^
 /langversion:5 ^
 /optimize+ ^
 /out:"%OUT%\TaxiMod.dll" ^
 %REFS% ^
 "%SRC%\*.cs"

set "EC=%ERRORLEVEL%"
echo.
if not "%EC%"=="0" (
  echo Compile FAILED. ExitCode=%EC%
  pause
  exit /b %EC%
)

echo Compile OK.
echo Output: "%OUT%\TaxiMod.dll"
pause
exit /b 0
