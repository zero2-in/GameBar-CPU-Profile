using System.Diagnostics;
using System.Security;
using System.Security.Cryptography;
using System.Text;

namespace GameBarCpuProfile;

public sealed class DatabaseSynchronizer
{
    private readonly AppSettings _settings = AppSettings.Load();
    private readonly HashSet<string> _failureHashes = new(StringComparer.Ordinal);

    // A named Mutex is owned by an OS thread, NOT by an async Task.
    // Never await between WaitOne() and ReleaseMutex(): after an await,
    // execution may resume on a different thread and ReleaseMutex throws.
    // Execute the entire mutex-protected operation on ONE thread-pool thread.
    // The asynchronous I/O operations may complete on other threads, but this
    // owning thread synchronously waits for them and releases its own mutex.
    public Task SyncAsync(CancellationToken cancellationToken) =>
        Task.Run(() => SyncOnSingleThread(cancellationToken), cancellationToken);

    private void SyncOnSingleThread(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Preserve the original global mutex name to serialize with other
        // instances of the service and --sync-once command.
        using var gate = new Mutex(false, @"Global\GameBarCpuProfileWriter");
        bool acquired;
        try { acquired = gate.WaitOne(TimeSpan.FromSeconds(10)); }
        catch (AbandonedMutexException) { acquired = true; }
        if (!acquired) throw new TimeoutException("Another SDB synchronization is running");
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var manifest = Manifest.Load();
            bool changed = Reconcile(manifest, GameBarRegistry.ScanLoadedUsers(), _settings.PruneRemoved);
            if (changed) manifest.Save();

            // These waits do not switch THIS thread: the mutex owner remains
            // blocked here until each asynchronous update has completed.
            SyncArchAsync("x64", manifest, cancellationToken).GetAwaiter().GetResult();
            SyncArchAsync("x86", manifest, cancellationToken).GetAwaiter().GetResult();
        }
        finally { gate.ReleaseMutex(); }
    }

    public static void SetExcluded(string path, bool excluded)
    {
        var fullPath = Path.GetFullPath(path);
        using var gate = new Mutex(false, @"Global\GameBarCpuProfileWriter");
        try { gate.WaitOne(); }
        catch (AbandonedMutexException) { /* ownership has been granted */ }
        try
        {
            var m = Manifest.Load();
            m.ExcludedPaths.RemoveAll(p => string.Equals(p, fullPath, StringComparison.OrdinalIgnoreCase));
            if (excluded) m.ExcludedPaths.Add(fullPath);
            if (excluded) m.Entries.RemoveAll(e => string.Equals(e.Path, fullPath, StringComparison.OrdinalIgnoreCase));
            m.Save();
            Log.Write((excluded ? "EXCLUDE " : "INCLUDE ") + fullPath);
        }
        finally { gate.ReleaseMutex(); }
    }

    private static bool Reconcile(Manifest state, Dictionary<string, HashSet<string>> scans, bool prune)
    {
        bool changed = false;
        var excluded = new HashSet<string>(state.ExcludedPaths, StringComparer.OrdinalIgnoreCase);
        var byPath = state.Entries.ToDictionary(e => e.Path, StringComparer.OrdinalIgnoreCase);
        var scannedSids = new HashSet<string>(scans.Keys, StringComparer.OrdinalIgnoreCase);

        if (prune)
        {
            // An unloaded user's records are never removed just because they
            // aren't accessible during this scan.
            foreach (var entry in state.Entries)
            {
                int removed = entry.OwnerSids.RemoveAll(sid => scannedSids.Contains(sid) &&
                    (!scans.TryGetValue(sid, out var paths) || !paths.Contains(entry.Path)));
                if (removed > 0) changed = true;
            }
        }

        foreach (var (sid, paths) in scans)
        {
            foreach (var path in paths)
            {
                if (excluded.Contains(path) || !File.Exists(path)) continue;
                if (!PeReader.TryRead(path, out var arch)) continue;

                if (!byPath.TryGetValue(path, out var entry))
                {
                    entry = new GameEntry { Path = path, Arch = arch };
                    state.Entries.Add(entry);
                    byPath[path] = entry;
                    changed = true;
                    Log.Write($"ADD [{arch}] {path}");
                }
                else if (entry.Arch != arch)
                {
                    entry.Arch = arch;
                    changed = true;
                    Log.Write("EXE ARCH CHANGED " + path);
                }
                if (!entry.OwnerSids.Contains(sid, StringComparer.OrdinalIgnoreCase))
                {
                    entry.OwnerSids.Add(sid);
                    changed = true;
                }
            }
        }

        if (prune)
        {
            int removed = state.Entries.RemoveAll(e => e.OwnerSids.Count == 0);
            if (removed > 0) { changed = true; Log.Write($"PRUNE {removed} registration(s)"); }
        }
        return changed;
    }

    private async Task SyncArchAsync(string arch, Manifest state, CancellationToken cancellationToken)
    {
        var db = arch == "x64" ? state.X64 : state.X86;
        var all = state.Entries.Where(e => e.Arch == arch).OrderBy(e => e.Path, StringComparer.OrdinalIgnoreCase).ToList();
        // Without SIZE, different paths with the same EXE filename cannot be
        // reliably distinguished by SDB matching. Exclude ALL colliding entries
        // rather than silently applying a shim to the wrong executable.
        var conflicting = all.GroupBy(e => Path.GetFileName(e.Path), StringComparer.OrdinalIgnoreCase)
                             .Where(g => g.Count() > 1)
                             .SelectMany(g => g).Select(e => e.Path)
                             .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var path in conflicting) Log.Write("AMBIGUOUS EXE (excluded from SDB): " + path);
        var entries = all.Where(e => !conflicting.Contains(e.Path)).ToList();
        string sdbPath = Path.Combine(DataPaths.Root, arch + ".sdb");
        string xmlPath = Path.Combine(DataPaths.Root, arch + ".xml");

        if (entries.Count == 0)
        {
            if (!db.Installed) return;
            var sdbinst = ToolLocator.FindSdbInst() ?? throw new FileNotFoundException("Windows sdbinst.exe is missing");
            var removed = await ToolLocator.RunAsync(sdbinst, ["-q", "-u", "-g", db.Guid], cancellationToken);
            if (removed.ExitCode != 0) throw new Exception($"Cannot uninstall empty {arch} SDB: {removed.Output}");
            db.Installed = false;
            db.InstalledHash = "";
            state.Save();
            Log.Write("UNINSTALLED empty " + arch + " SDB");
            return;
        }

        string xml = SdbXml.Build(arch, entries, db.Guid, _settings.ProcessorCount, _settings.DatabaseName);
        string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(xml)));
        if (db.Installed && string.Equals(db.InstalledHash, hash, StringComparison.Ordinal)) return;
        if (_failureHashes.Contains(arch + hash)) return;

        string? fixApp = ToolLocator.FindFixApp(_settings.FixAppPath);
        string? sdbInst = ToolLocator.FindSdbInst();
        if (fixApp is null || sdbInst is null)
        {
            Log.Write("SDB build pending: fixApp.exe or sdbinst.exe missing. Install ADK Application Compatibility Tools.");
            _failureHashes.Add(arch + hash);
            return;
        }

        var candidateXml = Path.Combine(DataPaths.Root, arch + ".pending.xml");
        var candidateSdb = Path.Combine(DataPaths.Root, arch + ".pending.sdb");
        try
        {
            await File.WriteAllTextAsync(candidateXml, xml, new UTF8Encoding(false), cancellationToken);
            if (File.Exists(candidateSdb)) File.Delete(candidateSdb);
            var compiled = await ToolLocator.RunAsync(fixApp, [candidateXml, candidateSdb], cancellationToken);
            if (compiled.ExitCode != 0 || !File.Exists(candidateSdb) || new FileInfo(candidateSdb).Length < 128)
                throw new InvalidDataException($"SDB compilation failed: {compiled.Output}");

            // Same database GUID as previous install. Never uninstall the old
            // database before a new candidate has compiled successfully.
            var installed = await ToolLocator.RunAsync(sdbInst, ["-q", candidateSdb], cancellationToken);
            if (installed.ExitCode != 0) throw new Exception($"SDB installation failed: {installed.Output}");
            if (File.Exists(sdbPath)) File.Copy(sdbPath, sdbPath + ".bak", overwrite: true);
            File.Copy(candidateSdb, sdbPath, overwrite: true);
            File.Copy(candidateXml, xmlPath, overwrite: true);
            db.Installed = true;
            db.InstalledHash = hash;
            state.Save();
            _failureHashes.Remove(arch + hash);
            Log.Write($"INSTALLED {arch} SDB: {entries.Count} games, ProcessorCountLie={_settings.ProcessorCount}");
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            _failureHashes.Add(arch + hash);
            Log.Write("BUILD/INSTALL FAILED " + arch + ": " + e);
            // Deliberately leave the previous installed database alone.
        }
    }

    public bool UninstallManagedDatabases()
    {
        using var gate = new Mutex(false, @"Global\GameBarCpuProfileWriter");
        try { gate.WaitOne(); }
        catch (AbandonedMutexException) { /* ownership acquired */ }
        try
        {
            var state = Manifest.Load();
            var sdbinst = ToolLocator.FindSdbInst();
            if (sdbinst is null) return false;
            bool success = true;
            foreach (var db in new[] { state.X64, state.X86 })
            {
                if (!db.Installed) continue;
                try
                {
                    var result = ToolLocator.RunAsync(sdbinst, ["-q", "-u", "-g", db.Guid], CancellationToken.None).GetAwaiter().GetResult();
                    if (result.ExitCode != 0) throw new Exception(result.Output);
                    db.Installed = false;
                    db.InstalledHash = "";
                    state.Save();
                }
                catch (Exception e) { success = false; Log.Write("UNINSTALL SDB FAILED: " + e); }
            }
            return success;
        }
        finally { gate.ReleaseMutex(); }
    }
}

public static class PeReader
{
    public static bool TryRead(string path, out string architecture)
    {
        architecture = "";
        try
        {
            using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new BinaryReader(stream);
            long length = stream.Length;
            if (reader.ReadUInt16() != 0x5A4D) return false;
            stream.Position = 0x3C;
            long offset = reader.ReadUInt32();
            if (offset < 64 || offset + 24 > length) return false;
            stream.Position = offset;
            if (reader.ReadUInt32() != 0x00004550) return false;
            architecture = reader.ReadUInt16() switch { 0x8664 => "x64", 0x014C => "x86", _ => "" };
            return architecture.Length > 0;
        }
        catch { return false; }
    }
}

public static class SdbXml
{
    public static string Build(string arch, IEnumerable<GameEntry> entries, string dbGuid, int count, string brand)
    {
        var b = new StringBuilder();
        var display = $"{brand} ({(arch == "x64" ? "64-bit Games" : "32-bit Games")})";
        b.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        b.AppendLine($"<DATABASE NAME=\"{Escape(display)}\" ID=\"{Escape(dbGuid)}\">");
        foreach (var game in entries)
        {
            string file = Path.GetFileName(game.Path);
            b.AppendLine($"  <APP NAME=\"{Escape(Path.GetFileNameWithoutExtension(file))}\" ID=\"{Escape(game.AppGuid)}\">");
            // Deliberately omit SIZE: game updates can change the binary length.
            // Preserve the per-entry GUID and architecture, and match by EXE name.
            b.AppendLine($"    <EXE NAME=\"{Escape(file)}\" ID=\"{Escape(game.ExeGuid)}\" RUNTIME_PLATFORM=\"{(arch == "x64" ? "AMD64" : "X86_ANY")}\">");
            b.AppendLine($"      <SHIM NAME=\"ProcessorCountLie\" COMMAND_LINE=\"{count}\" />");
            b.AppendLine("    </EXE>");
            b.AppendLine("  </APP>");
        }
        b.AppendLine("</DATABASE>");
        return b.ToString();
    }

    private static string Escape(string value) => SecurityElement.Escape(value) ?? "";
}
