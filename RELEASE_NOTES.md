# v0.1.0 — Initial Release

GameBar CPU Profile automatically applies per-game **ProcessorCountLie** compatibility fixes to games registered in Xbox Game Bar.

### Features

- Event-driven Windows service with no running-game polling
- Automatic Game Bar game registration and removal synchronization
- Configurable reported logical processor count (default: **16**)
- Separate, consolidated compatibility databases for x64 and x86 games
- Exclusion support, logs, and automatic service installation/uninstallation
- No CPU affinity changes, BIOS modifications, or reboot required for profile updates

### Requirements and limitations

- Windows 11 x64, administrator rights, Xbox Game Bar
- Windows ADK **Application Compatibility Tools** including `fixApp.exe` (not bundled)
- Restart affected games to use newly installed or removed fixes
- Not full CPU topology virtualization or guaranteed FPS improvement
- Use exclusions for games whose anti-cheat policies do not allow compatibility shims

Read [README.md](README.md) or [README.ko.md](README.ko.md) for installation and configuration.
