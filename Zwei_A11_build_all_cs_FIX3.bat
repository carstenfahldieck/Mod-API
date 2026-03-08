@echo off
setlocal EnableExtensions EnableDelayedExpansion

title CS1 LaneBalancer - BUILD (all cs)

set "SOURCE=G:\CS1_DEV\LaneBalancer"
set "TARGET=C:\Users\carst\AppData\Local\Colossal Order\Cities_Skylines\Addons\Mods\in Arbeit"
set "BUILD=C:\Users\carst\AppData\Local\CS1_LaneBalancer_Build"

set "CSC=C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe"
set "CS1=G:\SteamLibrary\steamapps\common\Cities_Skylines\Cities_Data\Managed"

echo ==========================================
echo   CS1 LaneBalancer - BUILD (all cs)
echo ==========================================
echo SOURCE : "%SOURCE%"
echo TARGET : "%TARGET%"
echo BUILD  : "%BUILD%"
echo CS1    : "%CS1%"
echo CSC    : "%CSC%"
echo.

if not exist "%CSC%" (
  echo [ERROR] csc.exe not found:
  echo         "%CSC%"
  echo.
  pause
  exit /b 1
)

for %%R in (
  "Assembly-CSharp.dll"
  "ColossalManaged.dll"
  "ICities.dll"
  "UnityEngine.dll"
  "UnityEngine.UI.dll"
) do (
  if not exist "%CS1%\%%~R" (
    echo [ERROR] Missing reference:
    echo         "%CS1%\%%~R"
    echo.
    pause
    exit /b 1
  )
)

if not exist "%SOURCE%" (
  echo [ERROR] SOURCE folder not found:
  echo         "%SOURCE%"
  echo.
  pause
  exit /b 1
)

if not exist "%TARGET%" (
  echo [ERROR] TARGET folder not found:
  echo         "%TARGET%"
  echo.
  pause
  exit /b 1
)

echo [INFO] Preparing local build folder...
if not exist "%BUILD%" mkdir "%BUILD%"
if errorlevel 1 (
  echo [ERROR] Could not create BUILD folder:
  echo         "%BUILD%"
  echo.
  pause
  exit /b 1
)

echo [INFO] Cleaning old build output...
if exist "%BUILD%\Arbeit.dll" del /f /q "%BUILD%\Arbeit.dll" >nul 2>&1

echo [INFO] Cleaning old Arbeit.dll in target...
if exist "%TARGET%\Arbeit.dll" del /f /q "%TARGET%\Arbeit.dll" >nul 2>&1

echo.
echo [INFO] Collecting all .cs files from SOURCE...
set "SRCFILES="
set "COUNT=0"

for %%F in ("%SOURCE%\*.cs") do (
  if exist "%%~fF" (
    set /a COUNT+=1
    set "SRCFILES=!SRCFILES! "%%~fF""
  )
)

if "!COUNT!"=="0" (
  echo [ERROR] No .cs files found in:
  echo         "%SOURCE%"
  echo.
  pause
  exit /b 1
)

echo [INFO] Source file count: !COUNT!
echo.

echo [INFO] Compiling...
echo.

"%CSC%" ^
  /nologo ^
  /target:library ^
  /out:"%BUILD%\Arbeit.dll" ^
  /reference:"%CS1%\Assembly-CSharp.dll" ^
  /reference:"%CS1%\ColossalManaged.dll" ^
  /reference:"%CS1%\ICities.dll" ^
  /reference:"%CS1%\UnityEngine.dll" ^
  /reference:"%CS1%\UnityEngine.UI.dll" ^
  !SRCFILES!

set "CSCERR=%ERRORLEVEL%"
echo.
echo [INFO] csc exit code: %CSCERR%

if not "%CSCERR%"=="0" (
  echo ==========================================
  echo BUILD FAILED
  echo ==========================================
  echo.
  pause
  exit /b %CSCERR%
)

if not exist "%BUILD%\Arbeit.dll" (
  echo [ERROR] Compiler reported success, but Arbeit.dll not found in BUILD.
  echo         "%BUILD%\Arbeit.dll"
  echo.
  pause
  exit /b 2
)

echo.
echo [INFO] Copying Arbeit.dll to target...
copy /y "%BUILD%\Arbeit.dll" "%TARGET%\Arbeit.dll" >nul
if errorlevel 1 (
  echo [ERROR] Copy failed to:
  echo         "%TARGET%\Arbeit.dll"
  echo.
  pause
  exit /b 3
)

echo.
echo ==========================================
echo BUILD SUCCESS
for %%I in ("%TARGET%\Arbeit.dll") do echo Size: %%~zI bytes
echo Output: "%TARGET%\Arbeit.dll"
echo Sources: !COUNT! file(s)
echo ==========================================
echo.
pause
exit /b 0
