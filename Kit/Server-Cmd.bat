@echo off
rem Send an admin command to the dedicated server running on this PC.
rem Usage: DSProto-Cmd.bat save | stop | quit | status
rem   save   = save the world now (same path as the in-game Save)    stop = save, then exit cleanly
rem   quit   = exit without saving                                    status = one status line in the server log
rem The server polls %APPDATA%\SpaceEngineers2.DedicatedServer\dsproto-cmd.txt once a second (log lines "[dsproto] admin ...").
setlocal
if "%~1"=="" (
  echo Usage: %~nx0 save^|stop^|quit^|status
  exit /b 1
)
set "CMDFILE=%APPDATA%\SpaceEngineers2.DedicatedServer\dsproto-cmd.txt"
if not exist "%APPDATA%\SpaceEngineers2.DedicatedServer" (
  echo No %APPDATA%\SpaceEngineers2.DedicatedServer folder: is the server running on this PC?
  exit /b 1
)
>>"%CMDFILE%" echo %~1
echo Sent "%~1" to %CMDFILE%
endlocal
