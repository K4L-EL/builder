using BetBuilder.Application.Interfaces;
using BetBuilder.Domain;

namespace BetBuilder.Application.Bingo;

public sealed record BingoCard(
    Guid Id,
    string Theme,
    string ThemeEmoji,
    string[] Legs,
    string[] DisplayLabels,
    decimal Odds,
    double JointProbability,
    decimal SuggestedStake,
    string FightId,
    string SnapshotId,
    DateTime GeneratedAtUtc);

public interface IBingoCardGenerator
{
    /// <summary>
    /// Auto-generate a set of bingo cards from the active snapshot. One card is produced
    /// per risk band (safe / medium / longshot / jackpot), up to <paramref name="count"/>,
    /// each with 3-5 legs. Odds are priced through <see cref="IComboPricingService"/>
    /// so the card reflects the same odds the player will see on the slip.
    /// </summary>
    IReadOnlyList<BingoCard> Generate(int count = 4, int? seed = null);
}

public sealed class BingoCardGenerator : IBingoCardGenerator
{
    private readonly IActiveSnapshotStore _store;
    private readonly IComboPricingService _pricing;

    private static readonly (string Theme, string Emoji, decimal MinOdds, decimal MaxOdds, int MinLegs, int MaxLegs)[] Bands =
    {
        ("SAFE BET",   "[S]", 1.40m,   2.50m, 3, 3),
        ("BANGER",     "[B]", 3.00m,   9.00m, 3, 4),
        ("LONGSHOT",   "[L]", 12.00m,  60.00m, 4, 5),
        ("JACKPOT",    "[J]", 80.00m,  800.00m, 5, 5)
    };

    public BingoCardGenerator(IActiveSnapshotStore store, IComboPricingService pricing)
    {
        _store = store;
        _pricing = pricing;
    }

    public IReadOnlyList<BingoCard> Generate(int count = 4, int? seed = null)
    {
        var snapshot = _store.GetActiveSnapshot();
        if (snapshot == null)
            return Array.Empty<BingoCard>();

        var rng = seed.HasValue ? new Random(seed.Value) : new Random();

        var available = snapshot.Legs
            .Where(l => !snapshot.UnavailableLegs.Contains(l))
            .Where(l =>
            {
                var idx = snapshot.LegIndexMap[l];
                var p = snapshot.LegProbabilities[idx];
                return p > 0.001 && p < 0.999;
            })
            .ToArray();

        if (available.Length < 3) return Array.Empty<BingoCard>();

        var cards = new List<BingoCard>(Math.Min(count, Bands.Length));
        var take = Math.Min(count, Bands.Length);

        for (var b = 0; b < take; b++)
        {
            var band = Bands[b];
            var card = TryBuildCard(snapshot, available, band, rng, attempts: 40);
            if (card != null) cards.Add(card);
        }

        return cards;
    }

    private BingoCard? TryBuildCard(
        PricingSnapshot snapshot,
        string[] available,
        (string Theme, string Emoji, decimal MinOdds, decimal MaxOdds, int MinLegs, int MaxLegs) band,
        Random rng,
        int attempts)
    {
        for (var a = 0; a < attempts; a++)
        {
            var legCount = rng.Next(band.MinLegs, band.MaxLegs + 1);
            if (legCount > available.Length) legCount = available.Length;

            var picks = PickDistinct(available, legCount, rng);
            var result = _pricing.Price(new ComboPricingRequest { Legs = picks });
            if (!result.Valid || result.PricedDecimalOdds is null) continue;

            var odds = (decimal)result.PricedDecimalOdds.Value;
            if (odds < band.MinOdds || odds > band.MaxOdds) continue;

            var suggested = SuggestedStake(odds);

            return new BingoCard(
                Id: Guid.NewGuid(),
                Theme: band.Theme,
                ThemeEmoji: band.Emoji,
                Legs: picks,
                DisplayLabels: picks.Select(PrettyLegLabel).ToArray(),
                Odds: odds,
                JointProbability: result.JointProbability ?? 0,
                SuggestedStake: suggested,
                FightId: string.IsNullOrWhiteSpace(snapshot.EventId) ? "default" : snapshot.EventId,
                SnapshotId: snapshot.SnapshotId,
                GeneratedAtUtc: DateTime.UtcNow);
        }

        return null;
    }

    private static decimal SuggestedStake(decimal odds) => odds switch
    {
        <= 2.5m => 25m,
        <= 10m  => 10m,
        <= 60m  => 5m,
        _        => 1m
    };

    private static string[] PickDistinct(string[] pool, int count, Random rng)
    {
        var copy = pool.ToArray();
        for (var i = copy.Length - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (copy[i], copy[j]) = (copy[j], copy[i]);
        }
        return copy.Take(count).ToArray();
    }

    private static string PrettyLegLabel(string leg)
    {
        var s = leg.StartsWith("bb_", StringComparison.OrdinalIgnoreCase) ? leg[3..] : leg;
        s = s.Replace('_', ' ');
        return string.Join(' ', s.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(w => w.Length == 0 ? w : char.ToUpperInvariant(w[0]) + w[1..]));
    }
}
