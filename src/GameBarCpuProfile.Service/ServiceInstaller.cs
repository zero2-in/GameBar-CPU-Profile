using System.Text.RegularExpressions;

namespace GameBarCpuProfile;

public static class ServiceInstaller
{
    public static int Run(string command)
    {
        var sc = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "sc.exe");
        if (!File.Exists(sc)) throw new FileNotFoundException(sc);
        if (command == "--stop-service") return Stop(sc) ? 0 : 2;
        if (command == "--uninstall-service")
        {
            if (!Stop(sc)) return 3;
            if (!new DatabaseSynchronizer().UninstallManagedDatabases())
            {
                Console.Error.WriteLine("Managed SDB uninstall failed. Service retained for safe retry.");
                return 4;
            }
            var delete = Execute(sc, ["delete", Program.ServiceName]);
            if (delete.ExitCode != 0 && Exists(sc)) return 5;
            Console.WriteLine("Service and managed compatibility databases removed.");
            return 0;
        }
        if (command == "--install-service")
        {
            string exe = Environment.ProcessPath ?? throw new InvalidOperationException("No executable path");
            if (!Exists(sc))
            {
                var create = Execute(sc, ["create", Program.ServiceName,
                    "binPath=", $"\"{exe}\"", "start=", "delayed-auto",
                    "DisplayName=", "GameBar CPU Profile Service"]);
                if (create.ExitCode != 0) { Console.Error.WriteLine(create.Output); return 2; }
            }
            else
            {
                var config = Execute(sc, ["config", Program.ServiceName, "binPath=", $"\"{exe}\"", "start=", "delayed-auto"]);
                if (config.ExitCode != 0) { Console.Error.WriteLine(config.Output); return 2; }
            }
            Execute(sc, ["description", Program.ServiceName, "Synchronizes Xbox Game Bar registrations with CPU compatibility profiles."]);
            var start = Execute(sc, ["start", Program.ServiceName]);
            if (start.ExitCode != 0 && !IsRunning(sc)) { Console.Error.WriteLine(start.Output); return 3; }
            Console.WriteLine("Service installed and started.");
            return 0;
        }
        throw new ArgumentException("Unknown service command " + command);
    }

    private static bool Stop(string sc)
    {
        if (!Exists(sc)) return true;
        if (IsStopped(sc)) return true;
        Execute(sc, ["stop", Program.ServiceName]);
        for (int i = 0; i < 60; i++)
        {
            if (IsStopped(sc)) return true;
            Thread.Sleep(500);
        }
        Console.Error.WriteLine("Service did not stop in time.");
        return false;
    }

    private static bool Exists(string sc) => Execute(sc, ["query", Program.ServiceName]).ExitCode == 0;
    private static bool IsRunning(string sc) => IsState(Execute(sc, ["query", Program.ServiceName]).Output, 4);
    private static bool IsStopped(string sc) => IsState(Execute(sc, ["query", Program.ServiceName]).Output, 1);

    private static bool IsState(string output, int state)
    {
        // sc.exe always includes the numeric service state; avoid relying on
        // English "RUNNING"/"STOPPED" names on Korean Windows installations.
        return Regex.IsMatch(output, @"(?im)^\s*(?:STATE|상태)\s*:\s*" + state + @"(?:\s|$)", RegexOptions.CultureInvariant);
    }

    private static (int ExitCode, string Output) Execute(string exe, IReadOnlyList<string> args) =>
        ToolLocator.RunAsync(exe, args, CancellationToken.None).GetAwaiter().GetResult();
}
