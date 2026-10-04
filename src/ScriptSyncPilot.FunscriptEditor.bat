@echo off
dotnet build "%~dp0ScriptSyncPilot.FunscriptEditor\ScriptSyncPilot.FunscriptEditor.csproj" --configuration Debug --nologo
if errorlevel 1 (
	pause
	exit /b 1
)
start "" "%~dp0ScriptSyncPilot.FunscriptEditor\bin\Debug\net9.0-windows\ScriptSyncPilot.FunscriptEditor.exe"
exit /b