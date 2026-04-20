using BetBuilder.Domain;

namespace BetBuilder.Application.Interfaces;

/// <summary>
/// Abstraction over the real-time push channel. Application/Infrastructure code talks to this
/// so it doesn't have to reference the SignalR types directly; the Api layer wires the
/// concrete implementation over a SignalR hub.
/// </summary>
public interface IFightBroadcaster
{
    Task StatsUpdate(string fightId, FightStatsSnapshot stats);
    Task SnapshotAdvanced(string fightId, string snapshotId, int currentIndex, int totalFiles, double elapsedSeconds);
    Task LegResolved(string fightId, string legName, LegOutcome outcome, double? finalProbability = null);
    Task TicketSettled(string userId, Guid ticketId, string result, decimal payout);
    Task FightResulted(string fightId, int legsResolved, int ticketsSettled, decimal totalPayout);
}

public enum LegOutcome
{
    Won,
    Lost,
    Void
}

/// <summary>
/// Minimal view over the stats feed so Application-layer services (resulting, etc.)
/// don't have to take an Infrastructure dependency.
/// </summary>
public interface IStatsFeedAccessor
{
    FightStatsSnapshot? Current { get; }
}
