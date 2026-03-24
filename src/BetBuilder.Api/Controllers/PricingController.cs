using BetBuilder.Api.Contracts;
using BetBuilder.Api.Mapping;
using BetBuilder.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace BetBuilder.Api.Controllers;

[ApiController]
[Route("api/v1/pricing")]
public sealed class PricingController : ControllerBase
{
    private readonly IComboPricingService _pricingService;

    public PricingController(IComboPricingService pricingService)
    {
        _pricingService = pricingService;
    }

    [HttpPost("combo")]
    [ProducesResponseType(typeof(ComboPricingApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public IActionResult PriceCombo([FromBody] ComboPricingApiRequest request)
    {
        var domainRequest = new ComboPricingRequest
        {
            EventId = request.EventId,
            SnapshotId = request.SnapshotId,
            Legs = request.Legs
        };

        var result = _pricingService.Price(domainRequest);
        return Ok(ResponseMapper.ToApiResponse(result));
    }
}
