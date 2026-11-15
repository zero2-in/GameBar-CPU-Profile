using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GameBarCpuProfile;

public static class Program
{
    public const string ServiceName = "GameBarCpuProfile";

    public static async Task<int> Main(string[] args)
    {
        try
        {
            var command = args.Length > 0 ? args[0].ToLowerInvariant() : "";
            if (command is "--install-service" or "--stop-service" or "--uninstall-service")
            {
                return ServiceInstaller.Run(command);
            }

            if (command == "--check-tools")
            {
                Console.WriteLine("sdbinst: " + (ToolLocator.FindSdbInst() ?? "NOT FOUND"));
                Console.WriteLine("fixApp:  " + (ToolLocator.FindFixApp(AppSettings.Load().FixAppPath) ?? "NOT FOUND (requires ADK Application Compatibility Tools)"));
                return ToolLocator.FindSdbInst() != null && ToolLocator.FindFixApp(AppSettings.Load().FixAppPath) != null ? 0 : 2;
            }

            if (command == "--sync-once")
            {
                await new DatabaseSynchronizer().SyncAsync(CancellationToken.None);
                return 0;
            }

            if (command == "--uninstall-databases")
            {
                return new DatabaseSynchronizer().UninstallManagedDatabases() ? 0 : 3;
            }

            if (command is "--exclude" or "--include")
            {
                if (args.Length < 2) throw new ArgumentException($"Usage: {command} <full path to EXE>");
                DatabaseSynchronizer.SetExcluded(args[1], command == "--exclude");
                Console.WriteLine("Saved. Run --sync-once or restart the service to apply now.");
                return 0;
            }

            if (command is not "" and not "--console")
            {
                Console.Error.WriteLine("Unknown argument: " + command);
                return 2;
            }

            var builder = Host.CreateApplicationBuilder(args: Array.Empty<string>());
            builder.Services.AddWindowsService(options => options.ServiceName = "GameBar CPU Profile Service");
            builder.Services.AddHostedService<GameBarWorker>();
            builder.Logging.SetMinimumLevel(LogLevel.Information);
            await builder.Build().RunAsync();
            return 0;
        }
        catch (Exception e)
        {
            Log.Write("FATAL " + e);
            Console.Error.WriteLine(e);
            return 1;
        }
    }
}
