SE2 dedicated server kit (DsProto 0.1, prototype) - Space Engineers 2 version 4.0.0.3155 only

SERVER (the server PC, no graphics card needed)
1. Space Engineers 2 installed (Steam is fine). Edit the GAME line in Start-Server.bat if it is not C:\SE2\Game.
2. Put this folder in a path WITHOUT spaces, e.g. C:\SE2\Kit.
3. Open UDP 27015 (router + Windows firewall).
4. First start: Start-Server.bat -startContent:Survival   Later: Start-Server.bat  (loads the last save)
5. Save: Server-Cmd.bat save    Stop (saves first): Server-Cmd.bat stop
   Log: %APPDATA%\SpaceEngineers2.DedicatedServer\Temp\Logs

PLAYERS
1. Put this folder somewhere WITHOUT spaces in the path. Steam running.
2. Double-click Join-Server.bat (or: Join-Server.bat IP:27015). Start the game this way, not from the SE2MP launcher.

LIMITS (prototype)
- This is Keen's own dedicated server made to run without graphics. It does NOT include SE2MP, so SE2MP's crash fixes
  and features are not active on this server.
- No password and no player limit: anyone with the address can join.
- Keen's dedicated server is unfinished; expect problems.
