# v0.1.1 — Mutex synchronization bug fix

This maintenance release fixes `System.ApplicationException` in `Mutex.ReleaseMutex()` during Game Bar synchronization.

- **Fixed:** The synchronization worker no longer acquires a named `Mutex` on one thread and releases it after asynchronous continuations may have changed the executing thread.
- **Unchanged:** Xbox Game Bar event monitoring, ProcessorCountLie settings, x64/x86 SDBs, SDB database GUIDs, manifest location and automatic pruning.
- **Upgrade:** Run the new installer over the existing installation (stop and replace service). The existing managed SDBs and configuration are retained.
- **Verification:** Register and unregister a test game, then check `C:\ProgramData\GameBarCpuProfile\service.log` for `Watcher error` or `ReleaseMutex` errors.

The fix has been reviewed at source level but must be built and validated on Windows.

---

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
