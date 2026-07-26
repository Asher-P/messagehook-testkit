@echo off
REM Build the React UI and bake it into the Playbook Service's wwwroot,
REM so `dotnet run --project MessageHook.Playbook.Service` serves the UI + API same-origin (no Vite dev server).
setlocal
set "ROOT=%~dp0"
set "UI=%ROOT%messagehook-ui"
set "WWWROOT=%ROOT%MessageHook.Playbook.Service\wwwroot"

echo [bake-ui] building React UI...
if not exist "%UI%\node_modules" (
  echo [bake-ui] node_modules missing -^> npm install
  call npm --prefix "%UI%" install || goto :err
)
call npm --prefix "%UI%" run build || goto :err

echo [bake-ui] copying dist -^> wwwroot
if exist "%WWWROOT%" rmdir /s /q "%WWWROOT%"
mkdir "%WWWROOT%"
xcopy "%UI%\dist\*" "%WWWROOT%\" /e /i /y >nul || goto :err

echo [bake-ui] done. Serve it with:
echo   dotnet run --project MessageHook.Playbook.Service
exit /b 0

:err
echo [bake-ui] FAILED
exit /b 1
