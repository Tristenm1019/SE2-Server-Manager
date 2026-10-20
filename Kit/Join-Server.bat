@echo off
rem Joins an SE2 dedicated server by address. Steam must be running. Put this folder somewhere WITHOUT spaces in the path.
rem Usage: Join-Server.bat 47.32.196.207:27015      (or double-click: joins the address below)
setlocal
set "SERVER=%~1"
if "%SERVER%"=="" set "SERVER=47.32.196.207:27015"
set "GAME="
for /f "tokens=2,*" %%a in ('reg query "HKLM\SOFTWARE\WOW6432Node\Valve\Steam\Apps\1133870" /v InstallLocation 2^>nul ^| find "InstallLocation"') do set "GAME=%%b"
if not defined GAME for /f "tokens=2,*" %%a in ('reg query "HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App 1133870" /v InstallLocation 2^>nul ^| find "InstallLocation"') do set "GAME=%%b"
if not defined GAME (
  echo Could not find Space Engineers 2. Edit this file: set "GAME=C:\...\steamapps\common\SpaceEngineers2"
  pause
  exit /b 1
)
set "SteamAppId=1133870"
set "SteamGameId=1133870"
start "" /D "%GAME%\Game2" "%GAME%\Game2\SpaceEngineers2.exe" -plugins:%~dp0DsProto.dll -dsproto:connect:%SERVER%
endlocal
