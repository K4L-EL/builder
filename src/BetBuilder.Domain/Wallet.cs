namespace BetBuilder.Domain;

public sealed class Wallet
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = default!;
    public decimal Balance { get; set; }
    public decimal Held { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public decimal Available => Balance - Held;
}
