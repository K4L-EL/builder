using System.Text.Json;
using BetBuilder.Application.Interfaces;
using BetBuilder.Domain;
using Microsoft.EntityFrameworkCore;

namespace BetBuilder.Infrastructure.Data;

public sealed class TicketService : ITicketService
{
    private readonly BetBuilderDbContext _db;
    private readonly IComboPricingService _pricingService;
    private readonly IWalletService _walletService;

    public TicketService(
        BetBuilderDbContext db,
        IComboPricingService pricingService,
        IWalletService walletService)
    {
        _db = db;
        _pricingService = pricingService;
        _walletService = walletService;
    }

    public async Task<Ticket> PlaceBet(PlaceBetRequest request)
    {
        if (request.Stake <= 0)
            throw new ArgumentException("Stake must be positive.");

        var pricingResult = _pricingService.Price(new ComboPricingRequest
        {
            Legs = request.Legs
        });

        if (!pricingResult.Valid)
            throw new InvalidOperationException(
                "Combo is not valid: " + string.Join("; ", pricingResult.Errors.Select(e => e.Message)));

        if (pricingResult.PricedDecimalOdds == null || pricingResult.PricedDecimalOdds <= 0)
            throw new InvalidOperationException("Combo has no valid priced odds.");

        var odds = (decimal)pricingResult.PricedDecimalOdds.Value;
        var potentialPayout = Math.Round(request.Stake * odds, 2);

        await _walletService.HoldStake(request.UserId, request.Stake);

        var ticket = new Ticket
        {
            Id = Guid.NewGuid(),
            UserId = request.UserId,
            SnapshotId = pricingResult.SnapshotId,
            LegsJson = JsonSerializer.Serialize(request.Legs),
            Stake = request.Stake,
            Odds = odds,
            PotentialPayout = potentialPayout,
            Status = TicketStatus.Placed,
            PlacedAt = DateTime.UtcNow
        };

        _db.Tickets.Add(ticket);
        await _db.SaveChangesAsync();

        return ticket;
    }

    public async Task<IReadOnlyList<Ticket>> GetUserTickets(string userId, int limit = 20)
    {
        return await _db.Tickets
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.PlacedAt)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<Ticket?> GetTicket(Guid ticketId)
    {
        return await _db.Tickets.FindAsync(ticketId);
    }

    public async Task<Ticket> Settle(Guid ticketId, TicketSettleResult result)
    {
        var ticket = await _db.Tickets.FindAsync(ticketId)
            ?? throw new KeyNotFoundException($"Ticket {ticketId} not found.");

        if (ticket.Status != TicketStatus.Placed)
            throw new InvalidOperationException($"Ticket {ticketId} is already settled ({ticket.Status}).");

        switch (result)
        {
            case TicketSettleResult.Won:
                ticket.Status = TicketStatus.Won;
                ticket.Payout = ticket.PotentialPayout;
                await _walletService.SettleWin(ticket.UserId, ticket.Stake, ticket.PotentialPayout);
                break;

            case TicketSettleResult.Lost:
                ticket.Status = TicketStatus.Lost;
                ticket.Payout = 0m;
                await _walletService.SettleLoss(ticket.UserId, ticket.Stake);
                break;

            case TicketSettleResult.Void:
                ticket.Status = TicketStatus.Void;
                ticket.Payout = ticket.Stake;
                await _walletService.ReleaseHold(ticket.UserId, ticket.Stake);
                break;
        }

        ticket.SettledAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return ticket;
    }
}
