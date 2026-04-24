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
        _logger.LogInformation("Applying database migrations into schema '{Schema}'...", BetBuilderDbContext.Schema);

        try
        {
            using var scope = _services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BetBuilderDbContext>();

            // Provision the schema before EF tries to create tables under it.
            // Skipped for "public" (always present). Identifier is quoted; the
            // value comes from a trusted operator-controlled env var.
            if (!string.Equals(BetBuilderDbContext.Schema, "public", StringComparison.OrdinalIgnoreCase))
            {
#pragma warning disable EF1002 // schema is regex-validated in BetBuilderDbContext.ResolveSchema
                await db.Database.ExecuteSqlRawAsync(
                    $"CREATE SCHEMA IF NOT EXISTS \"{BetBuilderDbContext.Schema}\";",
                    cancellationToken);
#pragma warning restore EF1002
            }

            await db.Database.EnsureCreatedAsync(cancellationToken);

            // Idempotent schema sync for columns added after the initial EnsureCreated.
            // Unqualified names resolve via the connection's Search Path.
            await db.Database.ExecuteSqlRawAsync(
                "ALTER TABLE tickets ADD COLUMN IF NOT EXISTS event_id varchar(128) NOT NULL DEFAULT 'default';",
                cancellationToken);
            await db.Database.ExecuteSqlRawAsync(
                "CREATE INDEX IF NOT EXISTS ix_tickets_event_id ON tickets(event_id);",
                cancellationToken);

            _logger.LogInformation("Database schema ready.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Could not connect to database. Wallet/ticket features will be unavailable " +
                "until DATABASE_URL or DB_HOST is configured.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
