@echo off
setlocal
dotnet build "%~dp0ScriptSyncPilot.FunscriptEditor\ScriptSyncPilot.FunscriptEditor.csproj" --configuration Debug --property:UseAppHost=true --nologo
if errorlevel 1 (
	pause
	exit /b 1
)
set "app="
for /f "delims=" %%I in ('dotnet msbuild "%~dp0ScriptSyncPilot.FunscriptEditor\ScriptSyncPilot.FunscriptEditor.csproj" "-property:Configuration=Debug" "-property:UseAppHost=true" -getProperty:TargetPath') do set "app=%%~dpnI.exe"
if not exist "%app%" (
	echo Could not locate the built application.
	pause
	exit /b 1
)
start "" "%app%"
exit /b