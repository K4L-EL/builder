using System.Collections.Concurrent;
using BetBuilder.Application.Interfaces;
using BetBuilder.Domain;

namespace BetBuilder.Infrastructure.State;

public sealed class ActiveSnapshotStore : IActiveSnapshotStore
{
    private readonly ConcurrentDictionary<string, PricingSnapshot> _snapshots = new();
    private volatile PricingSnapshot? _active;

    public PricingSnapshot? GetActiveSnapshot() => _active;

    public PricingSnapshot? GetSnapshot(string snapshotId)
    {
        _snapshots.TryGetValue(snapshotId, out var snapshot);
        return snapshot;
    }

    public IReadOnlyList<string> GetAllSnapshotIds() =>
        _snapshots.Keys.OrderBy(k => k).ToList();

    public void SetActiveSnapshot(string snapshotId)
    {
        if (!_snapshots.TryGetValue(snapshotId, out var snapshot))
            throw new KeyNotFoundException($"Snapshot '{snapshotId}' not found.");

        Interlocked.Exchange(ref _active, snapshot);
    }

    public void LoadSnapshot(PricingSnapshot snapshot)
    {
        _snapshots[snapshot.SnapshotId] = snapshot;
    }

    public void Clear()
    {
        _active = null;
        _snapshots.Clear();
    }
}
