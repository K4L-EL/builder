using System.Text.Json;
using BetBuilder.Application.Interfaces;
using BetBuilder.Domain;
using Microsoft.Extensions.Logging;

namespace BetBuilder.Application.Resulting;

public sealed record ResultingReport(
    string FightId,
    int LegsResolved,
    int TicketsSettled,
    decimal TotalPayout,
    IReadOnlyDictionary<string, string> LegOutcomes);

public interface IFightResultingService
{
    /// <summary>
    /// At end of fight, resolve every leg on the active snapshot from the latest stats,
    /// broadcast per-leg outcomes, then settle every open ticket for this event via
    /// <see cref="ITicketService.Settle"/> and broadcast per-user settlement events.
    /// </summary>
    Task<ResultingReport> ResolveFightAsync(string fightId, CancellationToken ct = default);
}

public sealed class FightResultingService : IFightResultingService
{
    private readonly IActiveSnapshotStore _store;
    private readonly ITicketService _tickets;
    private readonly ILegOutcomeResolver _resolver;
    private readonly IStatsFeedAccessor _statsAccessor;
    private readonly IFightBroadcaster _broadcaster;
    private readonly ILogger<FightResultingService> _logger;

    public FightResultingService(
        IActiveSnapshotStore store,
        ITicketService tickets,
        ILegOutcomeResolver resolver,
        IStatsFeedAccessor statsAccessor,
        IFightBroadcaster broadcaster,
        ILogger<FightResultingService> logger)
    {
        _store = store;
        _tickets = tickets;
        _resolver = resolver;
        _statsAccessor = statsAccessor;
        _broadcaster = broadcaster;
        _logger = logger;
    }

    public async Task<ResultingReport> ResolveFightAsync(string fightId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(fightId)) fightId = "default";

        var snapshot = _store.GetActiveSnapshot()
            ?? throw new InvalidOperationException("No active snapshot; cannot result fight.");

        var stats = _statsAccessor.Current;

        var perLeg = new Dictionary<string, LegOutcomeResult>(StringComparer.Ordinal);
        foreach (var leg in snapshot.Legs)
        {
            var result = _resolver.Resolve(leg, snapshot, stats);
            perLeg[leg] = result;
            try
            {
                await _broadcaster.LegResolved(fightId, leg, result.Outcome, result.FinalProbability);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "LegResolved broadcast failed for {Leg}", leg);
            }
        }

        var openTickets = await _tickets.GetOpenTicketsForEvent(fightId);
        var settledCount = 0;
        decimal totalPayout = 0m;

        foreach (var ticket in openTickets)
        {
            if (ct.IsCancellationRequested) break;

            var legs = JsonSerializer.Deserialize<string[]>(ticket.LegsJson) ?? Array.Empty<string>();
            var ticketResult = DetermineTicketResult(legs, perLeg);

            try
            {
                var settled = await _tickets.Settle(ticket.Id, ticketResult);
                settledCount++;
                totalPayout += settled.Payout ?? 0m;

                await _broadcaster.TicketSettled(
                    settled.UserId,
                    settled.Id,
                    ticketResult.ToString().ToLowerInvariant(),
                    settled.Payout ?? 0m);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to settle ticket {TicketId}", ticket.Id);
            }
        }

        var outcomeMap = perLeg.ToDictionary(
            kv => kv.Key,
            kv => kv.Value.Outcome.ToString().ToLowerInvariant());

        try
        {
            await _broadcaster.FightResulted(fightId, perLeg.Count, settledCount, totalPayout);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "FightResulted broadcast failed for {FightId}", fightId);
        }

        return new ResultingReport(fightId, perLeg.Count, settledCount, totalPayout, outcomeMap);
    }

    private static TicketSettleResult DetermineTicketResult(
        IReadOnlyList<string> legs,
        IReadOnlyDictionary<string, LegOutcomeResult> perLeg)
    {
        var hasVoid = false;
        foreach (var leg in legs)
        {
            if (!perLeg.TryGetValue(leg, out var outcome))
            {
                hasVoid = true;
                continue;
            }

            switch (outcome.Outcome)
            {
                case LegOutcome.Lost:
                    return TicketSettleResult.Lost;
                case LegOutcome.Void:
                    hasVoid = true;
                    break;
            }
        }

        return hasVoid ? TicketSettleResult.Void : TicketSettleResult.Won;
    }
}
