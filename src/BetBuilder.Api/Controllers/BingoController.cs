using System.ComponentModel.DataAnnotations;
using BetBuilder.Application.Bingo;
using BetBuilder.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace BetBuilder.Api.Controllers;

[ApiController]
[Route("api/v1/bingo")]
public sealed class BingoController : ControllerBase
{
    private readonly IBingoCardGenerator _generator;
    private readonly IBingoCardCache _cache;
    private readonly ITicketService _tickets;

    public BingoController(
        IBingoCardGenerator generator,
        IBingoCardCache cache,
        ITicketService tickets)
    {
        _generator = generator;
        _cache = cache;
        _tickets = tickets;
    }

    /// <summary>
    /// GET /api/v1/bingo/cards?fightId=...&amp;count=4 - auto-generates a fresh batch of cards
    /// from the currently active snapshot and caches them for bet placement.
    /// </summary>
    [HttpGet("cards")]
    public IActionResult Cards([FromQuery] string? fightId, [FromQuery] int count = 4, [FromQuery] int? seed = null)
    {
        if (count < 1) count = 1;
        if (count > 6) count = 6;

        var cards = _generator.Generate(count, seed);
        foreach (var card in cards)
            _cache.Store(card);

        return Ok(new
        {
            fightId = fightId ?? (cards.Count > 0 ? cards[0].FightId : "default"),
            count = cards.Count,
            cards = cards.Select(Map).ToArray()
        });
    }

    /// <summary>
    /// POST /api/v1/bingo/bet - place a ticket against a previously-served card id.
    /// Server-authoritative: legs come from the cached card, stake from the request.
    /// </summary>
    [HttpPost("bet")]
    public async Task<IActionResult> Bet([FromBody] BingoBetRequest request)
    {
        var card = _cache.Get(request.CardId);
        if (card == null)
            return NotFound(new ProblemDetails
            {
                Title = "Card expired",
                Detail = "Bingo card not found or expired; refresh the deck."
            });

        if (request.Stake <= 0)
            return BadRequest(new ProblemDetails { Title = "Invalid stake", Detail = "Stake must be positive." });

        try
        {
            var ticket = await _tickets.PlaceBet(new PlaceBetRequest
            {
                UserId = request.UserId,
                Legs = card.Legs,
                Stake = request.Stake,
                EventId = card.FightId
            });

            return Ok(new
            {
                ticketId = ticket.Id,
                cardId = card.Id,
                theme = card.Theme,
                legs = card.Legs,
                displayLabels = card.DisplayLabels,
                odds = ticket.Odds,
                stake = ticket.Stake,
                potentialPayout = ticket.PotentialPayout,
                fightId = ticket.EventId,
                placedAt = ticket.PlacedAt
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ProblemDetails { Title = "Invalid request", Detail = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ProblemDetails { Title = "Bet rejected", Detail = ex.Message });
        }
    }

    private static object Map(BingoCard c) => new
    {
        id = c.Id,
        theme = c.Theme,
        themeEmoji = c.ThemeEmoji,
        legs = c.Legs,
        displayLabels = c.DisplayLabels,
        odds = c.Odds,
        jointProbability = c.JointProbability,
        suggestedStake = c.SuggestedStake,
        fightId = c.FightId,
        snapshotId = c.SnapshotId,
        generatedAtUtc = c.GeneratedAtUtc
    };
}

public sealed class BingoBetRequest
{
    [Required] public string UserId { get; set; } = default!;
    [Required] public Guid CardId { get; set; }
    [Required] public decimal Stake { get; set; }
}
