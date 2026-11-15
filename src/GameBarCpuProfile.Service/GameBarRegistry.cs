using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Text.RegularExpressions;

namespace GameBarCpuProfile;

public static class GameBarRegistry
{
    private const string GameBarKey = @"System\GameConfigStore\Children";

    // HKEY_USERS may contain service profiles and _Classes hives. Do not read those.
    public static string[] LoadedUserSids()
    {
        using var users = RegistryKey.OpenBaseKey(RegistryHive.Users, RegistryView.Registry64);
        return users.GetSubKeyNames()
            .Where(s => (s.StartsWith("S-1-5-21-", StringComparison.OrdinalIgnoreCase) ||
                         s.StartsWith("S-1-12-1-", StringComparison.OrdinalIgnoreCase)) &&
                         !s.EndsWith("_Classes", StringComparison.OrdinalIgnoreCase))
            .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static Dictionary<string, HashSet<string>> ScanLoadedUsers()
    {
        var result = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        using var users = RegistryKey.OpenBaseKey(RegistryHive.Users, RegistryView.Registry64);
        foreach (var sid in LoadedUserSids())
        {
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using var children = users.OpenSubKey($@"{sid}\{GameBarKey}", writable: false);
                if (children != null)
                {
                    foreach (var child in children.GetSubKeyNames())
                    {
                        using var entry = children.OpenSubKey(child, writable: false);
                        if (entry?.GetValue("MatchedExeFullPath") is not string raw) continue;
                        try
                        {
                            var path = Path.GetFullPath(Environment.ExpandEnvironmentVariables(raw.Trim().Trim('"')));
                            if (Path.IsPathFullyQualified(path) && path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                                paths.Add(path);
                        }
                        catch (Exception e) { Log.Write("Invalid registered path skipped: " + e.Message); }
                    }
                }
            }
            catch (Exception e) { Log.Write($"Cannot scan Game Bar for user {sid}: {e.Message}"); continue; }
            result[sid] = paths;
        }
        return result;
    }

    // This method waits inside the Windows registry notification API. There is no
    // timed registry scan or monitoring of running game processes.
    public static void WaitForChange(CancellationToken cancellationToken)
    {
        using var users = RegistryKey.OpenBaseKey(RegistryHive.Users, RegistryView.Registry64);
        var keys = new List<RegistryKey> { users };
        var signals = new List<ManualResetEvent>();
        try
        {
            foreach (var sid in LoadedUserSids())
            {
                // If GameConfigStore hasn't been created yet, monitor the user's
                // hive for its creation. The root HKEY_USERS monitor detects logon.
                var key = OpenNotifyKey(users, $@"{sid}\System\GameConfigStore")
                          ?? OpenNotifyKey(users, sid);
                if (key != null) keys.Add(key);
            }

            if (keys.Count > 60) throw new InvalidOperationException("Too many loaded user registry hives");
            foreach (var key in keys)
            {
                var signal = new ManualResetEvent(false);
                signals.Add(signal);
                bool watchSubtree = !ReferenceEquals(key, users);
                int result = RegNotifyChangeKeyValue(key.Handle, watchSubtree,
                    0x10000005, signal.SafeWaitHandle, true);
                if (result != 0) throw new System.ComponentModel.Win32Exception(result,
                    "RegNotifyChangeKeyValue failed");
            }
            var handles = signals.Cast<WaitHandle>().Append(cancellationToken.WaitHandle).ToArray();
            int index = WaitHandle.WaitAny(handles);
            if (index == handles.Length - 1) cancellationToken.ThrowIfCancellationRequested();
        }
        finally
        {
            foreach (var key in keys) key.Dispose();
            foreach (var signal in signals) signal.Dispose();
        }
    }

    private static RegistryKey? OpenNotifyKey(RegistryKey root, string subkey)
    {
        try
        {
            return root.OpenSubKey(subkey, RegistryKeyPermissionCheck.ReadSubTree,
                RegistryRights.ReadKey);
        }
        catch { return null; }
    }

    [DllImport("advapi32.dll", ExactSpelling = true, SetLastError = true)]
    private static extern int RegNotifyChangeKeyValue(
        SafeRegistryHandle hKey,
        [MarshalAs(UnmanagedType.Bool)] bool watchSubtree,
        uint notifyFilter,
        SafeWaitHandle hEvent,
        [MarshalAs(UnmanagedType.Bool)] bool asynchronous);
}
