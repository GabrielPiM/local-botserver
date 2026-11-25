@echo off
cd /d "%~dp0"

rem Compilador do .NET Framework 4 (ja vem no Windows 8, 10 e 11) com as referencias do .NET 3.5:
rem o .exe sai para o CLR 2 e o LocalBotServer.exe.config faz ele abrir tambem no CLR 4.
rem 1684: tipo interno da Microsoft ausente nos DLLs de referencia, sem efeito aqui.
set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
set "REF=refs\net35"
set "OUT=bin"
if not exist "%CSC%" (
    echo [ERRO] .NET Framework 4 nao encontrado: "%CSC%".
    exit /b 1
)
if not exist "%REF%\mscorlib.dll" (
    echo [ERRO] Faltam as referencias em "%REF%". Veja refs/net35 no README.md.
    exit /b 1
)

if not exist "%OUT%" mkdir "%OUT%"
"%CSC%" /nologo /noconfig /nostdlib+ /codepage:65001 /langversion:3 /target:winexe /platform:anycpu /optimize+ /warnaserror+ /nowarn:1684 ^
  /out:"%OUT%\LocalBotServer.exe" /win32icon:src\icon.ico ^
  /resource:src\MainWindow.xaml,MainWindow.xaml /resource:src\icon.png,icon.png ^
  /resource:src\discord.png,discord.png /resource:src\youtube.png,youtube.png ^
  /resource:src\github.png,github.png ^
  /r:%REF%\mscorlib.dll /r:%REF%\System.dll /r:%REF%\System.Core.dll /r:%REF%\System.Xml.dll /r:%REF%\System.Web.Extensions.dll ^
  /r:%REF%\PresentationCore.dll /r:%REF%\PresentationFramework.dll /r:%REF%\WindowsBase.dll src\*.cs
if errorlevel 1 (
    echo [ERRO] Falha ao compilar.
    exit /b 1
)
rem Os dois arquivos andam juntos.
copy /y "src\LocalBotServer.exe.config" "%OUT%\" >nul
if errorlevel 1 (
    echo [ERRO] Falha ao copiar o LocalBotServer.exe.config.
    exit /b 1
)
echo [OK] %OUT%\LocalBotServer.exe
