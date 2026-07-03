@echo off
REM CC Signer — Windows 11 Publish Script
REM Requires: .NET 10.0 SDK (dotnet --version should show 10.x)
REM
REM Usage: publish-win.bat
REM Output: publish\win-x64\

echo === CC Signer — Windows 11 Publish ===
echo.

REM Check .NET SDK
dotnet --version >nul 2>&1
if %ERRORLEVEL% neq 0 (
    echo ERRO: .NET SDK nao encontrado.
    echo Instale de: https://dotnet.microsoft.com/download/dotnet/10.0
    exit /b 1
)

echo [1/3] A restaurar dependencias...
dotnet restore CC.Signer\CC.Signer.csproj
if %ERRORLEVEL% neq 0 exit /b 1

echo [2/3] A compilar para Windows x64...
dotnet publish CC.Signer\CC.Signer.csproj -c Release -r win-x64 --self-contained true -o publish\win-x64
if %ERRORLEVEL% neq 0 exit /b 1

echo [3/3] A limpar ficheiros temporarios...
if exist publish\win-x64\*.pdb del /q publish\win-x64\*.pdb

echo.
echo === Publish concluido ===
echo Output: %CD%\publish\win-x64\
echo Executavel: publish\win-x64\CC.Signer.exe
echo.
echo REQUISITOS para correr:
echo   1. Cartao de Cidadao middleware: https://www.autenticacao.gov.pt/cc-aplicacao
echo   2. OpenSC (pkcs11-tool): https://github.com/OpenSC/OpenSC/releases
echo   3. OpenSSL: https://slproweb.com/products/Win32OpenSSL.html
echo.
echo Copie a pasta publish\win-x64 para o computador Windows e execute CC.Signer.exe
