![GitHub all releases](https://img.shields.io/github/downloads/cosmii02/racingDSX/total)
![GitHub release](https://img.shields.io/github/v/release/Barnacules/RacingDSX)

Tested and confirmed to work with DSX v2 and v3.1

## Supported Games
- Forza Horizon 6 (NEW)
- Forza Horizon 5
- Forza Horizon 4
- Forza Motorsport 7 / 8
- DiRT Rally 1 / 2

---

## Building Locally (Windows 11 x64)

### Prerequisites

| Requirement | Version | Download |
|---|---|---|
| .NET SDK | 8.0 or newer | https://dotnet.microsoft.com/en-us/download/dotnet/8.0 |
| Git | any | https://git-scm.com/download/win |
| Windows | 10 / 11 (x64 or ARM64) | — |

> Visual Studio is **not** required. The SDK alone is sufficient.

### Quick build (one command)

Open **PowerShell** in the repository root and run:

```powershell
.\build.ps1
```

The script will:
1. Verify your .NET SDK version
2. Restore all NuGet packages
3. Publish a **self-contained, single-file** `RacingDSX.exe` for Windows x64

Output lands in:
```
build\win-x64\RacingDSX.exe
```

### Build script options

```powershell
# Standard release build (self-contained, ~170 MB, no runtime required to run)
.\build.ps1

# Debug build (faster compile, framework-dependent — .NET 8 runtime must be installed to run)
.\build.ps1 -Configuration Debug

# Release build without the "Press Enter to exit" prompt (useful in scripts)
.\build.ps1 -NoPause
```

### Manual build commands

If you prefer to run the dotnet CLI directly:

```powershell
# Restore packages
dotnet restore

# Release — self-contained single-file exe (matches GitHub CI output)
dotnet publish RacingDSX.csproj -c Release -r win-x64 `
    -p:PublishSingleFile=true `
    -p:SelfContained=true `
    -p:PublishReadyToRun=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    --output build\win-x64

# Debug — framework-dependent (requires .NET 8 runtime installed)
dotnet build RacingDSX.csproj -c Debug -r win-x64 --output build\win-x64-debug
```

### Opening in Visual Studio / Rider

Just open `RacingDSX.sln` — all dependencies restore automatically on first build.

### Running after a local build

```powershell
.\build\win-x64\RacingDSX.exe
```

The app stores its settings in `RacingDSX.json` next to the executable, so running from the build output folder keeps your configuration isolated from any installed copy.

> **Note for running pre-built releases:** The release zips from the [Releases page](https://github.com/Barnacules/RacingDSX/releases) are already self-contained — no separate .NET runtime install is needed to *run* those builds. The .NET SDK is only required when *building* from source.

---

🔺🔺 It is REQUIRED to install .NET8 for racingDSX to work at all!🔺🔺           
Download .NET8.0 from the link here: https://dotnet.microsoft.com/en-us/download



-----------------------------------------------------------------------------------------------------------------------------------------

# Step by step instructions for Forza Horizon 6:
1. Download latest version of RacingDSX from releases https://github.com/Barnacules/RacingDSX/releases
2. Extract the zip file to a folder (for example, the desktop)
3. Run RacingDSX.exe
4. Allow firewall prompts if they show up
5. Open DSX
6. Launch Forza Horizon 6
7. In-game, go to **Settings > HUD and Gameplay**
8. Enable **Data Out** and set **Data Out IP** to `127.0.0.1` and **Data Out IP Port** to `5300`
9. In DSX, make sure UDP is enabled: go to **Settings > Controller > Networking**. UDP must be enabled and listening on port `6969`

> **Note for Microsoft Store version of Forza Horizon 6:** If telemetry data is not being received, you may need to enable UDP loopback. Run the following command in PowerShell as Administrator:
> ```
> CheckNetIsolation LoopbackExempt -a -n="Microsoft.ForzaHorizon6_8wekyb3d8bbwe"
> ```
> Alternatively, install the [Windows 8 AppContainer Loopback Utility](https://telerik-fiddler.s3.amazonaws.com/fiddler/addons/enableloopbackutility.exe) and check Forza Horizon 6 in the list. The Steam version of FH6 does **not** require this step.

-----------------------------------------------------------------------------------------------------------------------------------------

# Setting up DiRT Rally 1 / 2 for UDP Connection:
1. Go to `C:\Users\<USER>\Documents\My Games\DiRT Rally X.0\hardwaresettings`;
2. Open `hardware_settings_config` file with your favorite text editor;
3. Find for **udp** tag and configure as shown below:
      ```xml
      <motion_platform>
           ...
           <udp enabled="true" extradata="3" ip="127.0.0.1" port="5300" delay="1" />
           ...
      </motion_platform>
      ```
   - **enabled = true**
   - **extradata = 3**
   - **port = 5300**

🔺🔺 Note for Forza Horizon 4 and Forza Motorsport 7 (THIS IS REQUIRED FOR IT TO WORK) 🔺🔺
1. Install [Window 8 AppContainer Loopback Utility](https://telerik-fiddler.s3.amazonaws.com/fiddler/addons/enableloopbackutility.exe)
2. Start the utility (if it shows a message about orphan sid, you can safely ignore it)
3. Make sure that Forza Horizon 4 / Motorsport 7 are checked
4. Save changes
In case the above do not work for you run the below command in Powershell as admin, the command enables udp loopback without needing the utility.

Forza Horizon 4: ```CheckNetIsolation LoopbackExempt -a -n="Microsoft.SunriseBaseGame_8wekyb3d8bbwe"```

Forza Motorsport 7: I do not have FM7 to get the ID, sorry :)

-----------------------------------------------------------------------------------------------------------------------------------------

Step by step instructions for Forza Horizon 5:
1. Download latest version of RacingDSX from releases https://github.com/Barnacules/RacingDSX/releases
2. extract the zip file to a folder (For example desktop)
3. Run RacingDSX exe
![image](https://user-images.githubusercontent.com/27782168/183417053-33676d94-f137-454b-ad7b-78066f71f6d2.png)
4. allow firewall prompts if they show up
5. Open DSX
6. Run forza horizon 5
7. Go to settings and enable DATA OUT and set ip to 127.0.0.1 and DATA OUT IP PORT to 5300
![image](https://user-images.githubusercontent.com/27782168/183418210-145b6701-f1f7-4783-91ba-7a1893294601.png)
8. In DSX make sure UDP is enabled, go to settings, click controller and then click networking. UDP has to be enabled and must be listening to port 6969


-----------------------------------------------------------------------------------------------------------------------------------------
Note for Forza Motorsport 7
1. Launch the game and head to the HUD options menu
2. Set Data Out to ON
3. Set Data Out IP Address to 127.0.0.1 (localhost)
4. Set Data Out IP Port to 5300
5. Set Data Out Packet Format to CAR DASH









## Thanks and Credits

[DualSenseX](https://github.com/Paliverse/DualSenseX)

[Forza-Telemetry](https://github.com/austinbaccus/forza-telemetry/tree/main/ForzaCore)

[patmagauran](https://github.com/patmagauran)
