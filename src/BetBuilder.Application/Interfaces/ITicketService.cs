using BetBuilder.Domain;

namespace BetBuilder.Application.Interfaces;

public interface ITicketService
{
    Task<Ticket> PlaceBet(PlaceBetRequest request);
    Task<IReadOnlyList<Ticket>> GetUserTickets(string userId, int limit = 20);
    Task<Ticket?> GetTicket(Guid ticketId);
    Task<IReadOnlyList<Ticket>> GetOpenTicketsForEvent(string eventId);
    Task<Ticket> Settle(Guid ticketId, TicketSettleResult result);
}

public sealed class PlaceBetRequest
{
    public string UserId { get; init; } = default!;
    public IReadOnlyList<string> Legs { get; init; } = Array.Empty<string>();
    public decimal Stake { get; init; }
    public string? EventId { get; init; }
}

public enum TicketSettleResult
{
    Won,
    Lost,
    Void
}
