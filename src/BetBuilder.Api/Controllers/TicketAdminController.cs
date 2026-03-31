using System.ComponentModel.DataAnnotations;
using BetBuilder.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace BetBuilder.Api.Controllers;

[ApiController]
[Route("api/v1/admin/tickets")]
public sealed class TicketAdminController : ControllerBase
{
    private readonly ITicketService _ticketService;
    private readonly ILogger<TicketAdminController> _logger;

    public TicketAdminController(ITicketService ticketService, ILogger<TicketAdminController> logger)
    {
        _ticketService = ticketService;
        _logger = logger;
    }

    [HttpPost("settle")]
    public async Task<IActionResult> Settle([FromBody] SettleTicketRequest request)
    {
        try
        {
            if (!Enum.TryParse<TicketSettleResult>(request.Result, true, out var result))
                return BadRequest(new ProblemDetails
                {
                    Title = "Invalid result",
                    Detail = "Result must be one of: won, lost, void"
                });

            var ticket = await _ticketService.Settle(request.TicketId, result);

            _logger.LogInformation(
                "Settled ticket {TicketId} as {Result}: payout={Payout}",
                ticket.Id, ticket.Status, ticket.Payout);

            return Ok(new
            {
                ticketId = ticket.Id,
                userId = ticket.UserId,
                status = ticket.Status.ToString().ToLower(),
                payout = ticket.Payout,
                settledAt = ticket.SettledAt
            });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ProblemDetails { Title = "Ticket not found", Detail = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ProblemDetails { Title = "Cannot settle", Detail = ex.Message });
        }
    }
}

public sealed class SettleTicketRequest
{
    [Required] public Guid TicketId { get; set; }
    [Required] public string Result { get; set; } = default!;
}
