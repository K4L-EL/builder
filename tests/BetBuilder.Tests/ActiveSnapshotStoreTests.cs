using BetBuilder.Infrastructure.State;

namespace BetBuilder.Tests;

public class ActiveSnapshotStoreTests
{
    [Fact]
    public void LoadAndGet_WorksCorrectly()
    {
        var store = new ActiveSnapshotStore();
        var snapshot = TestHelpers.CreateSnapshot(snapshotId: "ts0");

        store.LoadSnapshot(snapshot);

        var retrieved = store.GetSnapshot("ts0");
        Assert.NotNull(retrieved);
        Assert.Equal("ts0", retrieved!.SnapshotId);
    }

    [Fact]
    public void SetActiveSnapshot_SetsCorrectly()
    {
        var store = new ActiveSnapshotStore();
        store.LoadSnapshot(TestHelpers.CreateSnapshot(snapshotId: "ts0"));
        store.LoadSnapshot(TestHelpers.CreateSnapshot(snapshotId: "ts1"));

        store.SetActiveSnapshot("ts1");

        Assert.Equal("ts1", store.GetActiveSnapshot()!.SnapshotId);
    }

    [Fact]
    public void SetActiveSnapshot_ThrowsForMissingId()
    {
        var store = new ActiveSnapshotStore();

        Assert.Throws<KeyNotFoundException>(() => store.SetActiveSnapshot("missing"));
    }

    [Fact]
    public void GetAllSnapshotIds_ReturnsSorted()
    {
        var store = new ActiveSnapshotStore();
        store.LoadSnapshot(TestHelpers.CreateSnapshot(snapshotId: "ts2"));
        store.LoadSnapshot(TestHelpers.CreateSnapshot(snapshotId: "ts0"));
        store.LoadSnapshot(TestHelpers.CreateSnapshot(snapshotId: "ts1"));

        var ids = store.GetAllSnapshotIds();

        Assert.Equal(new[] { "ts0", "ts1", "ts2" }, ids);
    }

    [Fact]
    public void Clear_RemovesAllSnapshots()
    {
        var store = new ActiveSnapshotStore();
        store.LoadSnapshot(TestHelpers.CreateSnapshot(snapshotId: "ts0"));
        store.SetActiveSnapshot("ts0");

        store.Clear();

        Assert.Null(store.GetActiveSnapshot());
        Assert.Empty(store.GetAllSnapshotIds());
    }

    [Fact]
    public void AtomicSwap_PreservesReadability()
    {
        var store = new ActiveSnapshotStore();
        store.LoadSnapshot(TestHelpers.CreateSnapshot(snapshotId: "ts0"));
        store.LoadSnapshot(TestHelpers.CreateSnapshot(snapshotId: "ts1"));
        store.SetActiveSnapshot("ts0");

        var before = store.GetActiveSnapshot();
        store.SetActiveSnapshot("ts1");
        var after = store.GetActiveSnapshot();

        Assert.Equal("ts0", before!.SnapshotId);
        Assert.Equal("ts1", after!.SnapshotId);
    }
}
