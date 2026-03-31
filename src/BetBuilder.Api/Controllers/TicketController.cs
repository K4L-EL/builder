using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using BetBuilder.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace BetBuilder.Api.Controllers;

[ApiController]
[Route("api/v1/tickets")]
public sealed class TicketController : ControllerBase
{
    private readonly ITicketService _ticketService;

    public TicketController(ITicketService ticketService)
    {
        _ticketService = ticketService;
    }

    [HttpPost("place")]
    public async Task<IActionResult> Place([FromBody] PlaceTicketRequest request)
    {
        try
        {
            var ticket = await _ticketService.PlaceBet(new PlaceBetRequest
            {
                UserId = request.UserId,
                Legs = request.Legs,
                Stake = request.Stake
            });

            return Ok(MapTicket(ticket));
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

    [HttpGet("{userId}")]
    public async Task<IActionResult> ListUserTickets(string userId, [FromQuery] int limit = 20)
    {
        var tickets = await _ticketService.GetUserTickets(userId, Math.Min(limit, 100));
        return Ok(tickets.Select(MapTicket));
    }

    [HttpGet("ticket/{ticketId:guid}")]
    public async Task<IActionResult> GetTicket(Guid ticketId)
    {
        var ticket = await _ticketService.GetTicket(ticketId);
        if (ticket == null)
            return NotFound(new ProblemDetails { Title = "Ticket not found" });

        return Ok(MapTicket(ticket));
    }

    private static object MapTicket(Domain.Ticket t) => new
    {
        ticketId = t.Id,
        userId = t.UserId,
        snapshotId = t.SnapshotId,
        legs = JsonSerializer.Deserialize<string[]>(t.LegsJson) ?? Array.Empty<string>(),
        stake = t.Stake,
        odds = t.Odds,
        potentialPayout = t.PotentialPayout,
        status = t.Status.ToString().ToLower(),
        payout = t.Payout,
        placedAt = t.PlacedAt,
        settledAt = t.SettledAt
    };
}

public sealed class PlaceTicketRequest
{
    [Required] public string UserId { get; set; } = default!;
    [Required][MinLength(1)] public string[] Legs { get; set; } = Array.Empty<string>();
    [Required] public decimal Stake { get; set; }
}
