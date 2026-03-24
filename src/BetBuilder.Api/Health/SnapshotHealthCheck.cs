using BetBuilder.Application.Interfaces;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace BetBuilder.Api.Health;

public sealed class SnapshotHealthCheck : IHealthCheck
{
    private readonly IActiveSnapshotStore _store;

    public SnapshotHealthCheck(IActiveSnapshotStore store)
    {
        _store = store;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var active = _store.GetActiveSnapshot();
        if (active == null)
            return Task.FromResult(HealthCheckResult.Unhealthy("No active snapshot loaded."));

        var data = new Dictionary<string, object>
        {
            ["snapshotId"] = active.SnapshotId,
            ["legCount"] = active.LegCount,
            ["scenarioCount"] = active.ScenarioCount
        };

        return Task.FromResult(HealthCheckResult.Healthy(
            $"Active: {active.SnapshotId}, {active.LegCount} legs, {active.ScenarioCount} scenarios",
            data));
    }
}
