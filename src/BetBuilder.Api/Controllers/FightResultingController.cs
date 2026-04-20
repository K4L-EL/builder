using BetBuilder.Application.Resulting;
using Microsoft.AspNetCore.Mvc;

namespace BetBuilder.Api.Controllers;

[ApiController]
[Route("api/v1/admin/fights")]
public sealed class FightResultingController : ControllerBase
{
    private readonly IFightResultingService _resulter;

    public FightResultingController(IFightResultingService resulter)
    {
        _resulter = resulter;
    }

    /// <summary>
    /// Manually trigger resulting for a fight. Also auto-runs at end of simulation.
    /// </summary>
    [HttpPost("{eventId}/result")]
    public async Task<IActionResult> Result(string eventId, CancellationToken ct)
    {
        try
        {
            var report = await _resulter.ResolveFightAsync(eventId, ct);
            return Ok(report);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ProblemDetails { Title = "Cannot result fight", Detail = ex.Message });
        }
    }
}
