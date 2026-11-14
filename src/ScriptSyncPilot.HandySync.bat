@echo off
setlocal
set "artifacts=%~dp0..\artifacts\local-run\ScriptSyncPilot.HandySync"
set "appdir=%artifacts%\publish"
set "app=%appdir%\ScriptSyncPilot.HandySync.exe"
dotnet publish "%~dp0ScriptSyncPilot.HandySync\ScriptSyncPilot.HandySync.csproj" --configuration Debug --property:UseAppHost=true --artifacts-path "%artifacts%\build" --output "%appdir%" --nologo
if errorlevel 1 (
	pause
	exit /b 1
)
if not exist "%app%" (
	echo Could not locate the built application.
	pause
	exit /b 1
)
start "" /D "%appdir%" "%app%"
exit /b
