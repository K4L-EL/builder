using BetBuilder.Api.Contracts;
using BetBuilder.Api.Mapping;
using BetBuilder.Application.Interfaces;
using BetBuilder.Infrastructure.Snapshots;
using Microsoft.AspNetCore.Mvc;

namespace BetBuilder.Api.Controllers;

[ApiController]
[Route("api/v1/snapshots")]
public sealed class SnapshotsController : ControllerBase
{
    private readonly IActiveSnapshotStore _store;

    public SnapshotsController(IActiveSnapshotStore store)
    {
        _store = store;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<SnapshotInfoResponse>), StatusCodes.Status200OK)]
    public IActionResult ListSnapshots()
    {
        var ids = _store.GetAllSnapshotIds();
        var infos = ids
            .Select(id => _store.GetSnapshot(id))
            .Where(s => s != null)
            .Select(s => ResponseMapper.ToSnapshotInfo(s!))
            .ToList();

        return Ok(infos);
    }

    [HttpGet("active")]
    [ProducesResponseType(typeof(SnapshotInfoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetActive()
    {
        var snapshot = _store.GetActiveSnapshot();
        if (snapshot == null)
            return NotFound(new ProblemDetails { Title = "No active snapshot." });

        return Ok(ResponseMapper.ToSnapshotInfo(snapshot));
    }
}
