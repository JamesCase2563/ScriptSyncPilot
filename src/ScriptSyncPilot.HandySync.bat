@echo off
dotnet build "%~dp0ScriptSyncPilot.HandySync\ScriptSyncPilot.HandySync.csproj" --configuration Debug --nologo
if errorlevel 1 (
	pause
	exit /b 1
)
start "" "%~dp0ScriptSyncPilot.HandySync\bin\Debug\net9.0-windows\ScriptSyncPilot.HandySync.exe"
exit /b
