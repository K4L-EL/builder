using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BetBuilder.Infrastructure.Data;

public sealed class DatabaseMigrationService : IHostedService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<DatabaseMigrationService> _logger;

    public DatabaseMigrationService(IServiceProvider services, ILogger<DatabaseMigrationService> logger)
    {
        _services = services;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Applying database migrations...");

        try
        {
            using var scope = _services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BetBuilderDbContext>();
            await db.Database.EnsureCreatedAsync(cancellationToken);
            _logger.LogInformation("Database schema ready.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Could not connect to database. Wallet/ticket features will be unavailable " +
                "until DATABASE_URL is configured.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
