using BetBuilder.Application.Interfaces;
using BetBuilder.Infrastructure.Snapshots;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BetBuilder.Infrastructure.Hosting;

public sealed class SnapshotLoaderService : IHostedService
{
    private readonly ISnapshotSource _source;
    private readonly IPricingSnapshotFactory _factory;
    private readonly IActiveSnapshotStore _store;
    private readonly DataSettings _settings;
    private readonly ILogger<SnapshotLoaderService> _logger;

    public SnapshotLoaderService(
        ISnapshotSource source,
        IPricingSnapshotFactory factory,
        IActiveSnapshotStore store,
        IOptions<DataSettings> settings,
        ILogger<SnapshotLoaderService> logger)
    {
        _source = source;
        _factory = factory;
        _store = store;
        _settings = settings.Value;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Loading pricing snapshots...");

        var groups = _source.DiscoverSnapshots();
        if (groups.Count == 0)
        {
            _logger.LogWarning(
                "No snapshot file groups found in data directory. " +
                "The API will start without pre-loaded snapshots. " +
                "Use POST /api/v1/admin/snapshots/upload to push snapshot data.");
            return Task.CompletedTask;
        }

        foreach (var group in groups)
        {
            var snapshot = _factory.Build(group);
            _store.LoadSnapshot(snapshot);
        }

        var defaultId = _settings.DefaultSnapshot;
        if (_store.GetSnapshot(defaultId) != null)
        {
            _store.SetActiveSnapshot(defaultId);
            _logger.LogInformation("Active snapshot set to {SnapshotId}", defaultId);
        }
        else
        {
            var firstId = _store.GetAllSnapshotIds().First();
            _store.SetActiveSnapshot(firstId);
            _logger.LogWarning(
                "Default snapshot {Default} not found, activated {Fallback} instead",
                defaultId, firstId);
        }

        _logger.LogInformation(
            "Loaded {Count} snapshots: [{Ids}]",
            _store.GetAllSnapshotIds().Count,
            string.Join(", ", _store.GetAllSnapshotIds()));

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
