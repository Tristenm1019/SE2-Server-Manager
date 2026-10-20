@echo off
rem SE2 dedicated server, no graphics (DsProto prototype). Listens on UDP 27015.
rem First start:  Start-Server.bat -startContent:Survival      (new Survival world)
rem Later starts: Start-Server.bat -startLast                  (the last saved world)
rem Log: %APPDATA%\SpaceEngineers2.DedicatedServer\Temp\Logs (lines with [dsproto]). Save/stop: Server-Cmd.bat save|stop
setlocal
set "GAME=C:\SE2\Game"
if not exist "%GAME%\Game2\SpaceEngineers2.exe" (
  echo Space Engineers 2 not found in %GAME%: edit the GAME line in this file.
  exit /b 1
)
set "SteamAppId=1133870"
set "SteamGameId=1133870"
set "ARGS=%*"
if "%ARGS%"=="" set "ARGS=-startLast"
start "" /D "%GAME%\Game2" "%GAME%\Game2\SpaceEngineers2.exe" -allowMultiple -localDedicatedServer -nosplash -hiddenWindow -plugins:%~dp0DsProto.dll -dsproto:headless -dsproto:bind:0.0.0.0:27015 -dsproto:name:SE2MP_Server -dsproto:noreport %ARGS%
echo Server starting (no window). Check the log in %APPDATA%\SpaceEngineers2.DedicatedServer\Temp\Logs
endlocal
