using System.Diagnostics;

namespace GameBarCpuProfile;

public static class ToolLocator
{
    public static string? FindSdbInst()
    {
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "sdbinst.exe");
        return File.Exists(path) ? path : null;
    }

    public static string? FindFixApp(string configuredPath)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath)) return File.Exists(configuredPath) ? configuredPath : null;
        string rel = @"Windows Kits\10\Assessment and Deployment Kit";
        var roots = new[] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles) }.Distinct();
        foreach (var root in roots)
        {
            var adk = Path.Combine(root, rel);
            foreach (var candidate in new[] {
                Path.Combine(adk, @"Application Compatibility Toolkit\Standard User Analyzer\fixApp.exe"),
                Path.Combine(adk, @"Application Compatibility Tools\Standard User Analyzer\fixApp.exe")
            }) if (File.Exists(candidate)) return candidate;
            if (!Directory.Exists(adk)) continue;
            try
            {
                return Directory.EnumerateFiles(adk, "fixApp.exe",
                    new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true })
                    .FirstOrDefault();
            }
            catch { /* Try another installation root. */ }
        }
        return null;
    }

    public static async Task<(int ExitCode, string Output)> RunAsync(string path, IReadOnlyList<string> args, CancellationToken token)
    {
        using var process = new Process();
        process.StartInfo.FileName = path;
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.CreateNoWindow = true;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        foreach (var arg in args) process.StartInfo.ArgumentList.Add(arg);
        if (!process.Start()) throw new InvalidOperationException("Unable to launch: " + path);
        try
        {
            var stdout = process.StandardOutput.ReadToEndAsync(token);
            var stderr = process.StandardError.ReadToEndAsync(token);
            await process.WaitForExitAsync(token);
            return (process.ExitCode, (await stdout + "\n" + await stderr).Trim());
        }
        catch
        {
            if (!process.HasExited) { try { process.Kill(entireProcessTree: true); } catch { } }
            throw;
        }
    }
}
