@echo off
setlocal EnableExtensions
chcp 65001 >nul
title FerrariPOS IA - Publicar proyecto completo
cd /d "%~dp0"
echo ============================================================
echo FerrariPOS - PUBLICAR PROYECTO COMPLETO EN UN REPOSITORIO
echo Incluye Windows + Worker Cloudflare + workflows GitHub
echo ============================================================
echo.
set "REPO_URL="
set /p "REPO_URL=Pegá la URL del repositorio FerrariPOS-Asistente-IA: "
if "%REPO_URL%"=="" (echo No ingresaste la URL.& pause & exit /b 1)
where git >nul 2>&1
if errorlevel 1 (echo ERROR: Git no está instalado. Instalá Git for Windows y volvé a ejecutar este BAT.& pause & exit /b 1)
if not exist ".github\workflows\build.yml" (echo ERROR: Falta .github\workflows\build.yml. Extraé el ZIP completo, no copies solo el BAT.& pause & exit /b 1)
if not exist "FerrariPOS.Windows\FerrariPOS.csproj" (echo ERROR: Falta el código fuente Windows. Extraé el ZIP completo, no copies solo el BAT.& pause & exit /b 1)
if not exist "Cloudflare\FerrariPOS-Asistente-IA\wrangler.toml" (echo ERROR: Falta el Worker de Cloudflare.& pause & exit /b 1)
echo.
echo Se publicará TODO el contenido de esta carpeta en:
echo %REPO_URL%
echo Esto actualizará archivos del repositorio de destino. No se eliminan carpetas locales.
choice /M "¿Continuar"
if errorlevel 2 (echo Cancelado.& pause & exit /b 0)
if not exist .git git init
 git branch -M main >nul 2>&1
 git add -A
 git diff --cached --quiet
if not errorlevel 1 (echo No hay cambios nuevos para subir.& pause & exit /b 0)
git commit -m "FerrariPOS: Windows EXE y validacion/despliegue Worker Groq" || (echo ERROR al crear commit. Revisá la salida anterior.& pause & exit /b 1)
git remote get-url origin >nul 2>&1
if errorlevel 1 (git remote add origin "%REPO_URL%") else (git remote set-url origin "%REPO_URL%")
git push -u origin main
if errorlevel 1 (echo.
echo ERROR al subir. Comprobá que la URL sea correcta y que tengas permisos en GitHub.
echo Si el repositorio ya tiene archivos/commits, puede requerir reconciliar los historiales antes de subir.
pause
exit /b 1)
echo.
echo PUBLICACION TERMINADA. Abri Actions en GitHub y revisa los dos trabajos.
echo Cuando termine Windows, descarga FerrariPOS-Windows-EXE y FerrariPOS-Windows-Setup.
echo Los secretos CLOUDFLARE_API_TOKEN y CLOUDFLARE_ACCOUNT_ID deben estar configurados en el repositorio.
pause
