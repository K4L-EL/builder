using BetBuilder.Application.Interfaces;
using BetBuilder.Domain;
using Microsoft.AspNetCore.SignalR;

namespace BetBuilder.Api.Hubs;

/// <summary>
/// SignalR hub that broadcasts live fight updates scoped by <c>fightId</c>.
/// Clients call <see cref="JoinFight"/> to subscribe to a specific fight group;
/// the server pushes stats, snapshot advances, leg resolutions, and settlements.
/// </summary>
public sealed class FightHub : Hub
{
    public const string Path = "/hubs/fight";

    public async Task JoinFight(string fightId)
    {
        if (string.IsNullOrWhiteSpace(fightId)) fightId = "default";
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupFor(fightId));
    }

    public async Task LeaveFight(string fightId)
    {
        if (string.IsNullOrWhiteSpace(fightId)) fightId = "default";
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupFor(fightId));
    }

    public async Task JoinUser(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId)) return;
        await Groups.AddToGroupAsync(Context.ConnectionId, UserGroupFor(userId));
    }

    public async Task LeaveUser(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId)) return;
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, UserGroupFor(userId));
    }

    public static string GroupFor(string fightId) => $"fight:{fightId}";
    public static string UserGroupFor(string userId) => $"user:{userId}";
}

/// <summary>
/// SignalR-backed implementation of <see cref="IFightBroadcaster"/>. Application and
/// Infrastructure code depend only on the interface; this adapter lives in the Api layer
/// because it references the hub context.
/// </summary>
public sealed class FightHubBroadcaster : IFightBroadcaster
{
    private readonly IHubContext<FightHub> _hub;

    public FightHubBroadcaster(IHubContext<FightHub> hub)
    {
        _hub = hub;
    }

    public Task StatsUpdate(string fightId, FightStatsSnapshot stats) =>
        _hub.Clients.Group(FightHub.GroupFor(fightId)).SendAsync("statsUpdate", new
        {
            fightId,
            snapshotId = stats.SnapshotId,
            eventId = stats.EventId,
            elapsedSeconds = stats.ElapsedSeconds,
            capturedAtUtc = stats.CapturedAtUtc,
            metrics = stats.Metrics,
            legResults = stats.LegResults
        });

    public Task SnapshotAdvanced(string fightId, string snapshotId, int currentIndex, int totalFiles, double elapsedSeconds) =>
        _hub.Clients.Group(FightHub.GroupFor(fightId)).SendAsync("snapshotAdvanced", new
        {
            fightId,
            snapshotId,
            currentIndex,
            totalFiles,
            elapsedSeconds
        });

    public Task LegResolved(string fightId, string legName, LegOutcome outcome, double? finalProbability = null) =>
        _hub.Clients.Group(FightHub.GroupFor(fightId)).SendAsync("legResolved", new
        {
            fightId,
            leg = legName,
            outcome = outcome.ToString().ToLowerInvariant(),
            finalProbability
        });

    public Task TicketSettled(string userId, Guid ticketId, string result, decimal payout) =>
        _hub.Clients.Group(FightHub.UserGroupFor(userId)).SendAsync("ticketSettled", new
        {
            userId,
            ticketId,
            result,
            payout
        });

    public Task FightResulted(string fightId, int legsResolved, int ticketsSettled, decimal totalPayout) =>
        _hub.Clients.Group(FightHub.GroupFor(fightId)).SendAsync("fightResulted", new
        {
            fightId,
            legsResolved,
            ticketsSettled,
            totalPayout
        });
}
