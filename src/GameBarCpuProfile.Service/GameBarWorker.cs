using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GameBarCpuProfile;

public sealed class GameBarWorker(ILogger<GameBarWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var manager = new DatabaseSynchronizer();
        logger.LogInformation("GameBar CPU Profile started. Event-based registry monitoring.");
        Log.Write("Service started; no game-process polling.");

        // Initial reconciliation only when starting; then only registry events.
        try { await manager.SyncAsync(stoppingToken); }
        catch (Exception e) { logger.LogError(e, "Initial synchronization failed"); Log.Write(e.ToString()); }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Run(() => GameBarRegistry.WaitForChange(stoppingToken), stoppingToken);
                await Task.Delay(AppSettings.Load().DebounceMilliseconds, stoppingToken);
                await manager.SyncAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception e)
            {
                logger.LogError(e, "Registry watcher or sync error");
                Log.Write("Watcher error: " + e);
                try { await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); }
                catch (OperationCanceledException) { break; }
            }
        }
        Log.Write("Service stopped.");
    }
}
