using Microsoft.Extensions.Hosting;
using NoteSphere.Api.Data;

namespace NoteSphere.Api.Services;

public sealed class PurgeDeletedNotesHostedService : BackgroundService
{
    private readonly IServiceProvider _sp;
    private readonly ILogger<PurgeDeletedNotesHostedService> _logger;
    private const int RetentionDays = 30;

    public PurgeDeletedNotesHostedService(IServiceProvider sp, ILogger<PurgeDeletedNotesHostedService> logger)
    {
        _sp = sp;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Run shortly after startup, then once per day.
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _sp.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<NoteRepository>();
                var deleted = await repo.PurgeExpiredAsync(RetentionDays);
                if (deleted > 0)
                    _logger.LogInformation("Purged {Count} soft-deleted notes older than {Days} days.", deleted, RetentionDays);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Purge job failed.");
            }

            await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
        }
    }
}
