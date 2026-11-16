@echo off
setlocal
set "artifacts=%~dp0..\artifacts\local-run\ScriptSyncPilot.FunscriptEditor"
set "appdir=%artifacts%\publish"
set "app=%appdir%\ScriptSyncPilot.FunscriptEditor.exe"
dotnet publish "%~dp0ScriptSyncPilot.FunscriptEditor\ScriptSyncPilot.FunscriptEditor.csproj" --configuration Debug --property:UseAppHost=true --artifacts-path "%artifacts%\build" --output "%appdir%" --nologo
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