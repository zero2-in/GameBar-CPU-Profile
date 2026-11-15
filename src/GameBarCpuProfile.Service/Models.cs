using System.Text.Json;

namespace GameBarCpuProfile;

public static class DataPaths
{
    public static readonly string Root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "GameBarCpuProfile");
    public static readonly string State = Path.Combine(Root, "manifest.json");
    public static readonly string Settings = Path.Combine(Root, "settings.json");
    public static readonly string LogFile = Path.Combine(Root, "service.log");
    public static void Ensure() => Directory.CreateDirectory(Root);
}

public sealed class AppSettings
{
    public int ProcessorCount { get; set; } = 16;
    public bool PruneRemoved { get; set; } = true;
    public int DebounceMilliseconds { get; set; } = 450;
    public string DatabaseName { get; set; } = "GameBar CPU Profile";
    public string FixAppPath { get; set; } = "";

    public static AppSettings Load()
    {
        DataPaths.Ensure();
        if (!File.Exists(DataPaths.Settings))
        {
            var sample = new AppSettings();
            File.WriteAllText(DataPaths.Settings, JsonSerializer.Serialize(sample, JsonOptions.Pretty));
            return sample;
        }
        var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(DataPaths.Settings), JsonOptions.Default)
            ?? throw new InvalidDataException("settings.json is empty");
        if (settings.ProcessorCount is < 1 or > 32) throw new InvalidDataException("ProcessorCount must be 1..32");
        if (settings.DebounceMilliseconds is < 100 or > 10000) throw new InvalidDataException("DebounceMilliseconds must be 100..10000");
        if (string.IsNullOrWhiteSpace(settings.DatabaseName)) throw new InvalidDataException("DatabaseName cannot be empty");
        return settings;
    }
}

public static class JsonOptions
{
    public static readonly JsonSerializerOptions Default = new() { PropertyNameCaseInsensitive = true };
    public static readonly JsonSerializerOptions Pretty = new() { WriteIndented = true };
}

public sealed class DatabaseInfo
{
    public string Guid { get; set; } = System.Guid.NewGuid().ToString("B").ToUpperInvariant();
    public bool Installed { get; set; }
    public string InstalledHash { get; set; } = "";
}

public sealed class GameEntry
{
    public string Path { get; set; } = "";
    public string Arch { get; set; } = "";
    public long Size { get; set; }
    public string AppGuid { get; set; } = System.Guid.NewGuid().ToString("B").ToUpperInvariant();
    public string ExeGuid { get; set; } = System.Guid.NewGuid().ToString("B").ToUpperInvariant();
    public List<string> OwnerSids { get; set; } = new();
}

public sealed class Manifest
{
    public int Version { get; set; } = 1;
    public DatabaseInfo X64 { get; set; } = new();
    public DatabaseInfo X86 { get; set; } = new();
    public List<GameEntry> Entries { get; set; } = new();
    public List<string> ExcludedPaths { get; set; } = new();

    public static Manifest Load()
    {
        DataPaths.Ensure();
        if (!File.Exists(DataPaths.State)) return new Manifest();
        var manifest = JsonSerializer.Deserialize<Manifest>(File.ReadAllText(DataPaths.State), JsonOptions.Default)
            ?? throw new InvalidDataException("manifest.json is empty");
        if (manifest.Version != 1) throw new InvalidDataException("Unknown manifest version: " + manifest.Version);
        return manifest;
    }

    public void Save()
    {
        DataPaths.Ensure();
        var temp = DataPaths.State + ".new";
        File.WriteAllText(temp, JsonSerializer.Serialize(this, JsonOptions.Pretty));
        if (File.Exists(DataPaths.State)) File.Copy(DataPaths.State, DataPaths.State + ".bak", overwrite: true);
        File.Move(temp, DataPaths.State, overwrite: true);
    }
}

public static class Log
{
    private static readonly object Gate = new();
    public static void Write(string message)
    {
        try
        {
            lock (Gate)
            {
                DataPaths.Ensure();
                if (File.Exists(DataPaths.LogFile) && new FileInfo(DataPaths.LogFile).Length > 2_000_000)
                    File.Move(DataPaths.LogFile, DataPaths.LogFile + ".old", overwrite: true);
                File.AppendAllText(DataPaths.LogFile, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}{Environment.NewLine}");
            }
        }
        catch { /* Logging failures must never crash the Windows Service. */ }
    }
}
