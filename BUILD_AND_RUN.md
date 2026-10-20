# SE2 Dedicated Server Manager / Hosting Edition

## Requirements
- Windows 10/11 x64
- .NET 8 SDK to build the source (the published self-contained EXE does not need .NET installed)
- Space Engineers 2 **4.0.0.3155 only**
- DsProto 0.1 dedicated server prototype

## Build on Windows (PowerShell)

Open PowerShell in this folder:

```powershell
dotnet restore
dotnet publish .\SE2ServerManager.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o C:\SE2\Manager
```

Launch `C:\SE2\Manager\SE2ServerManager.exe`.

## Kit location

The archive includes the **original uploaded kit** under `Kit` (unchanged). The prototype requires a **kit path without spaces**. Copy the included `Kit` folder to `C:\SE2\Kit` and set the manager's Kit Directory to that path. Do not simply run the original batch files: the manager invokes the server executable directly with equivalent DsProto arguments, to track the process. The game path is the folder containing `Game2\SpaceEngineers2.exe`; it may be inside a normal Steam library.

## How to use

1. Enter game directory and `C:\SE2\Kit`, save settings.
2. Set name and UDP port (default 27015).
3. Use **NEW WORLD** for first Survival start, then **START LAST** afterward.
4. Save/Stop/Status/Force Quit send the original DsProto commands through the same `dsproto-cmd.txt` file as `Server-Cmd.bat`.
5. Logs automatically follow the newest `.log` or `.txt` in `%APPDATA%\SpaceEngineers2.DedicatedServer\Temp\Logs`.
6. Forward UDP 27015 on the router and allow it through Windows Firewall manually; inspection button does not open ports.

## Known limitations

- This is source code ready for building on Windows; this package is **not a compiled/Windows-tested binary**.
- Prototype can crash or fail independently of the manager. The process indicator is not a guaranteed game-ready/externally-reachable health check.
- The managed server will continue running if the manager window is closed, but the application does not attach automatically to an already running process; restart management from a clean server launch. Only control one local server instance.
- Player counts are not shown because current kit does not expose reliable player enumeration to this app.
- No SE2MP features, password, or player cap.
- Changing port/name requires server restart.
- The log viewer uses most recently modified log and reads incrementally; logging may include other lines than DsProto's if they share its output folder.
