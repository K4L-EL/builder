using BetBuilder.Domain;

namespace BetBuilder.Application.Interfaces;

public interface IActiveSnapshotStore
{
    PricingSnapshot? GetActiveSnapshot();
    PricingSnapshot? GetSnapshot(string snapshotId);
    IReadOnlyList<string> GetAllSnapshotIds();
    void SetActiveSnapshot(string snapshotId);
    void LoadSnapshot(PricingSnapshot snapshot);
    void Clear();
}
