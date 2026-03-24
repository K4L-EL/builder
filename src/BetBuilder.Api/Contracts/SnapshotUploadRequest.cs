using System.ComponentModel.DataAnnotations;

namespace BetBuilder.Api.Contracts;

public sealed class SnapshotUploadRequest
{
    [Required]
    public string SnapshotId { get; set; } = default!;

    [Required]
    public string OutcomeMatrixCsv { get; set; } = default!;

    /// <summary>
    /// Optional. If omitted, leg probabilities are derived from the outcome matrix.
    /// </summary>
    public string? LegProbsCsv { get; set; }

    /// <summary>
    /// Optional. If omitted, correlation matrix is left empty (metadata only).
    /// </summary>
    public string? CorrelationMatrixCsv { get; set; }

    public string? EventId { get; set; }
    public string? ModelVersion { get; set; }

    /// <summary>
    /// If true, this snapshot becomes the active pricing snapshot immediately.
    /// Defaults to true for in-play use.
    /// </summary>
    public bool Activate { get; set; } = true;
}
