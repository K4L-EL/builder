using BetBuilder.Api.Contracts;
using BetBuilder.Api.Mapping;
using BetBuilder.Application.Interfaces;
using BetBuilder.Infrastructure.Snapshots;
using Microsoft.AspNetCore.Mvc;

namespace BetBuilder.Api.Controllers;

[ApiController]
[Route("api/v1/admin/snapshots")]
public sealed class SnapshotAdminController : ControllerBase
{
    private readonly IActiveSnapshotStore _store;
    private readonly ISnapshotSource _source;
    private readonly IPricingSnapshotFactory _factory;
    private readonly ILogger<SnapshotAdminController> _logger;

    public SnapshotAdminController(
        IActiveSnapshotStore store,
        ISnapshotSource source,
        IPricingSnapshotFactory factory,
        ILogger<SnapshotAdminController> logger)
    {
        _store = store;
        _source = source;
        _factory = factory;
        _logger = logger;
    }

    /// <summary>
    /// Upload new simulation data and build a snapshot in-memory.
    /// This is the primary in-play update mechanism -- the model pipeline
    /// POSTs new outcome matrices here whenever the model recalculates.
    /// Only outcomeMatrixCsv is required; legProbsCsv and correlationMatrixCsv are optional.
    /// </summary>
    [HttpPost("upload")]
    [ProducesResponseType(typeof(SnapshotInfoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public IActionResult Upload([FromBody] SnapshotUploadRequest request)
    {
        var content = new SnapshotCsvContent
        {
            SnapshotId = request.SnapshotId,
            OutcomeMatrixCsv = request.OutcomeMatrixCsv,
            LegProbsCsv = request.LegProbsCsv,
            CorrelationMatrixCsv = request.CorrelationMatrixCsv,
            EventId = request.EventId,
            ModelVersion = request.ModelVersion
        };

        var snapshot = _factory.BuildFromContent(content);
        _store.LoadSnapshot(snapshot);

        if (request.Activate)
        {
            _store.SetActiveSnapshot(snapshot.SnapshotId);
            _logger.LogInformation(
                "Uploaded and activated snapshot {SnapshotId}: {LegCount} legs, {ScenarioCount} scenarios",
                snapshot.SnapshotId, snapshot.LegCount, snapshot.ScenarioCount);
        }
        else
        {
            _logger.LogInformation(
                "Uploaded snapshot {SnapshotId} (not activated): {LegCount} legs, {ScenarioCount} scenarios",
                snapshot.SnapshotId, snapshot.LegCount, snapshot.ScenarioCount);
        }

        return Ok(ResponseMapper.ToSnapshotInfo(snapshot));
    }

    /// <summary>
    /// Upload a binary-packed outcome matrix. ~15x smaller than CSV for large simulations.
    /// The model pipeline should prefer this endpoint for high-frequency in-play updates.
    /// </summary>
    [HttpPost("upload/binary")]
    [ProducesResponseType(typeof(SnapshotInfoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public IActionResult UploadBinary([FromBody] BinarySnapshotUploadRequest request)
    {
        byte[] packed;
        try { packed = Convert.FromBase64String(request.PackedRowsBase64); }
        catch (FormatException)
        {
            return BadRequest(new ProblemDetails { Title = "Invalid base64", Detail = "PackedRowsBase64 is not valid base64." });
        }

        var content = new SnapshotBinaryContent
        {
            SnapshotId = request.SnapshotId,
            Legs = request.Legs,
            PackedRows = packed,
            ScenarioCount = request.ScenarioCount,
            EventId = request.EventId,
            ModelVersion = request.ModelVersion
        };

        var snapshot = _factory.BuildFromBinary(content);
        _store.LoadSnapshot(snapshot);

        if (request.Activate)
        {
            _store.SetActiveSnapshot(snapshot.SnapshotId);
            _logger.LogInformation(
                "Uploaded (binary) and activated snapshot {SnapshotId}: {LegCount} legs, {ScenarioCount} scenarios",
                snapshot.SnapshotId, snapshot.LegCount, snapshot.ScenarioCount);
        }
        else
        {
            _logger.LogInformation(
                "Uploaded (binary) snapshot {SnapshotId} (not activated): {LegCount} legs, {ScenarioCount} scenarios",
                snapshot.SnapshotId, snapshot.LegCount, snapshot.ScenarioCount);
        }

        return Ok(ResponseMapper.ToSnapshotInfo(snapshot));
    }

    [HttpPost("activate/{snapshotId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public IActionResult Activate(string snapshotId)
    {
        try
        {
            _store.SetActiveSnapshot(snapshotId);
            _logger.LogInformation("Active snapshot changed to {SnapshotId}", snapshotId);
            return Ok(new { activated = snapshotId });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Snapshot not found",
                Detail = $"Snapshot '{snapshotId}' does not exist."
            });
        }
    }

    [HttpPost("reload")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public IActionResult Reload()
    {
        _logger.LogInformation("Reloading all snapshots from disk...");

        var currentActive = _store.GetActiveSnapshot()?.SnapshotId;
        var groups = _source.DiscoverSnapshots();

        _store.Clear();

        foreach (var group in groups)
        {
            var snapshot = _factory.Build(group);
            _store.LoadSnapshot(snapshot);
        }

        if (currentActive != null && _store.GetSnapshot(currentActive) != null)
        {
            _store.SetActiveSnapshot(currentActive);
        }
        else if (_store.GetAllSnapshotIds().Count > 0)
        {
            _store.SetActiveSnapshot(_store.GetAllSnapshotIds().First());
        }

        _logger.LogInformation("Reloaded {Count} snapshots", _store.GetAllSnapshotIds().Count);

        return Ok(new
        {
            reloaded = _store.GetAllSnapshotIds().Count,
            active = _store.GetActiveSnapshot()?.SnapshotId
        });
    }
}
