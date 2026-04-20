using System.Collections.Concurrent;

namespace BetBuilder.Application.Bingo;

/// <summary>
/// Short-lived cache of generated bingo cards so clients can place a bet referencing
/// a cardId they received; odds stay authoritative server-side for the TTL window.
/// </summary>
public interface IBingoCardCache
{
    void Store(BingoCard card);
    BingoCard? Get(Guid cardId);
}

public sealed class BingoCardCache : IBingoCardCache
{
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(90);

    private readonly ConcurrentDictionary<Guid, (BingoCard Card, DateTime ExpiresAtUtc)> _entries = new();

    public void Store(BingoCard card)
    {
        Evict();
        _entries[card.Id] = (card, DateTime.UtcNow.Add(Ttl));
    }

    public BingoCard? Get(Guid cardId)
    {
        if (!_entries.TryGetValue(cardId, out var entry)) return null;
        if (entry.ExpiresAtUtc < DateTime.UtcNow)
        {
            _entries.TryRemove(cardId, out _);
            return null;
        }
        return entry.Card;
    }

    private void Evict()
    {
        var now = DateTime.UtcNow;
        foreach (var kv in _entries)
        {
            if (kv.Value.ExpiresAtUtc < now)
                _entries.TryRemove(kv.Key, out _);
        }
    }
}
