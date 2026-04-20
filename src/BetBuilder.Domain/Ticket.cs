namespace BetBuilder.Domain;

public enum TicketStatus
{
    Placed,
    Won,
    Lost,
    Void,
    Cancelled
}

public sealed class Ticket
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = default!;
    public string SnapshotId { get; set; } = default!;
    public string EventId { get; set; } = "default";
    public string LegsJson { get; set; } = "[]";
    public decimal Stake { get; set; }
    public decimal Odds { get; set; }
    public decimal PotentialPayout { get; set; }
    public TicketStatus Status { get; set; }
    public decimal? Payout { get; set; }
    public DateTime PlacedAt { get; set; }
    public DateTime? SettledAt { get; set; }
}
