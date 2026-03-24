namespace BetBuilder.Infrastructure.Snapshots;

public interface ISnapshotSource
{
    IReadOnlyList<SnapshotFileGroup> DiscoverSnapshots();
}
