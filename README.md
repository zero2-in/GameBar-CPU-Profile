# GameBar CPU Profile

**Per-game CPU count profiles for Windows, automatically managed through Xbox Game Bar.**

GameBar CPU Profile is a lightweight Windows service that applies the Windows **`ProcessorCountLie`** application compatibility fix to games you mark in Xbox Game Bar. It helps compatible games **detect a user-selected logical processor count** without globally disabling CPU cores or rebooting.

Built with dual-CCD Ryzen X3D processors in mind, but not limited to AMD hardware.

> [!IMPORTANT]
> This is a **CPU-count compatibility fix, not CPU virtualization**. It does not change the host CPU topology, force threads onto a particular CCD, or guarantee higher FPS. Effects depend on how each game detects CPUs and manages its threads.

## How is it different from CPU affinity or CPU Sets?

| | CPU affinity / CPU Sets | GameBar CPU Profile |
|---|---|---|
| Primary effect | Limits or guides *where* threads run | Changes the CPU count *reported through supported compatibility-shimmed API calls* |
| Can influence engine worker-thread initialization? | Depends on the engine | Yes, when the engine uses covered processor-info APIs |
| Physically disables cores? | No | No |
| Changes CPU affinity? | Affinity does; CPU Sets uses a separate API | **No** (`ProcessorCountLie`, not `ProcessorCountLie2`) |
| Requires a reboot to change profiles? | No | No; restart the affected game |

On a Ryzen 9 7950X3D, for example, you can keep both CCDs enabled while asking a compatible game to detect **16 logical processors** instead of 32. The host Windows installation and other applications retain access to the full CPU.

## Features

- **Automatic Game Bar integration:** Watches games added to or removed from the Xbox Game Bar list.
- **Event-driven Windows service:** Waits for registry-change notifications; does not poll running games, FPS, or CPU utilization.
- **One database per architecture:** Maintains a combined `x64.sdb` and `x86.sdb` instead of a database for every game.
- **Automatic updates and removal:** Rebuilds and installs the affected database when registrations change; removes obsolete fixes when a game is unregistered.
- **Configurable CPU count:** Defaults to `ProcessorCountLie=16`.
- **Per-game exclusions:** Exclude games that are incompatible or should keep their default CPU detection.

## Requirements

- Windows 11 **x64** and administrator rights for installation.
- **Microsoft Windows ADK — Application Compatibility Tools** component, including a working `fixApp.exe` installation, to build the compatibility databases. The installer **does not install ADK**.
- Xbox Game Bar game registration (typically **Win + G → Remember this is a game**).
- The release installer contains a **self-contained .NET 10 service**; a separate .NET runtime is not required.

> [!NOTE]
> SDB compilation currently uses an **undocumented `fixApp.exe` command-line interface**. It has been tested in the development environment but may vary across ADK versions. This tool is not affiliated with AMD, Microsoft, or Xbox.

## Install and use

1. Install Microsoft's [Windows ADK](https://learn.microsoft.com/en-us/windows-hardware/get-started/adk-install), selecting **Application Compatibility Tools**.
2. Download the latest **`GameBarCpuProfile-Setup-…-x64.exe`** from the repository **Releases** page and run it as administrator.
3. Launch a game and mark it as a game in **Xbox Game Bar** (Win + G).
4. The service adds the compatibility fix automatically. **Exit and relaunch the game** for the change to take effect.
5. Uncheck **Remember this is a game** to remove its auto-managed fix when Game Bar updates the registration.

The service runs in the background as **GameBar CPU Profile Service**. It uses the registered game's EXE, not a running-game process watcher.

### Verify installation

Open **Services** (`services.msc`) and locate **GameBar CPU Profile Service**. You can also run these commands from an *elevated* PowerShell terminal:

```powershell
sc.exe query GameBarCpuProfile
& "$env:ProgramFiles\GameBar CPU Profile\GameBarCpuProfile.Service.exe" --check-tools
```

For application compatibility databases, use **Compatibility Administrator (64-bit)** or **(32-bit)** from the ADK. Open the corresponding SDB file:

```text
C:\ProgramData\GameBarCpuProfile\x64.sdb
C:\ProgramData\GameBarCpuProfile\x86.sdb
```

Changes to a game's reported CPU count are **not reflected in Windows Task Manager's system-wide CPU count**.

## Configuration

The service creates its configuration under:

```text
C:\ProgramData\GameBarCpuProfile\
├── settings.json
├── manifest.json
├── x64.xml / x64.sdb
├── x86.xml / x86.sdb
└── service.log
```

Default settings:

```json
{
  "ProcessorCount": 16,
  "PruneRemoved": true,
  "DebounceMilliseconds": 450,
  "DatabaseName": "GameBar CPU Profile",
  "FixAppPath": ""
}
```

Edit `settings.json` using administrator privileges and restart **GameBarCpuProfile** for changes to take effect. `ProcessorCount` is a **logical processor count**, not a physical core count or CCD selector.

**Exclude or re-include a game** using an elevated terminal (replace the example path):

```powershell
& "$env:ProgramFiles\GameBar CPU Profile\GameBarCpuProfile.Service.exe" --exclude "D:\Games\Example.exe"
Restart-Service GameBarCpuProfile

# Undo the exclusion:
& "$env:ProgramFiles\GameBar CPU Profile\GameBarCpuProfile.Service.exe" --include "D:\Games\Example.exe"
Restart-Service GameBarCpuProfile
```

Check `C:\ProgramData\GameBarCpuProfile\service.log` if registration or SDB installation does not work.

## Uninstall

Use **Windows Settings → Apps → Installed apps → GameBar CPU Profile → Uninstall**. The uninstaller stops the service, removes **only the compatibility databases managed by this service**, and removes the service itself. Diagnostic data may remain in `C:\ProgramData\GameBarCpuProfile`.

## Limitations and safety

- **Not all games use the CPU detection APIs covered by `ProcessorCountLie`.** Thread counts and game performance may remain unchanged.
- The fix does not spoof every CPU-topology property (physical core count, caches, CPUID, etc.) or pin the game to the V-Cache CCD.
- **Game compatibility and anti-cheat:** Do not automatically apply compatibility shims to games whose anti-cheat policies prohibit them; use an exclusion or do not register such titles.
- The SDB is registered **system-wide**. Matching uses the executable **filename without a file-size condition**, so game updates do not invalidate the fix merely by changing file size. Unrelated EXEs with the same name may also match. When two registered games of the same architecture share an EXE filename, both are excluded from that architecture's SDB and logged as `AMBIGUOUS EXE`.
- Some games may already have `ProcessorCountLie` fixes in separate AMD/Windows compatibility databases. If multiple databases target the same EXE, the resulting effective value must be verified rather than assuming a precedence order; consider `--exclude`.
- An active game needs a **restart** to use a newly installed or removed compatibility fix.
- Game Bar's registry layout and undocumented ADK compilation behavior may change with Windows/ADK updates.

## For developers

This repository contains the C# Windows service, Inno Setup installer, and PowerShell build script. See [Developer guide (한국어)](docs/DEVELOPMENT.ko.md) for local builds, service commands, architecture, and testing.

```powershell
.\scripts\build-installer.ps1
```

Licensed under the [MIT License](LICENSE). Contributions and reproducible compatibility reports are welcome.
