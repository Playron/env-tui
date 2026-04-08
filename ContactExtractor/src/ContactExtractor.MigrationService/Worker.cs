using ContactExtractor.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace ContactExtractor.MigrationService;

public class Worker(
    IServiceProvider serviceProvider,
    IHostApplicationLifetime hostLifetime,
    ILogger<Worker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Applying EF Core migrations...");

        using var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await db.Database.MigrateAsync(stoppingToken);

        logger.LogInformation("Migrations applied successfully.");
        hostLifetime.StopApplication();
    }
}