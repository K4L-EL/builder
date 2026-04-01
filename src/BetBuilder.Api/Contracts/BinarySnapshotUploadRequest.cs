using System.ComponentModel.DataAnnotations;

namespace BetBuilder.Api.Contracts;

public sealed class BinarySnapshotUploadRequest
{
    [Required]
    public string SnapshotId { get; set; } = default!;

    [Required]
    public string[] Legs { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Base64-encoded packed bitfield matrix.
    /// Each scenario row is ceil(legCount/8) bytes; bit N = leg N.
    /// Total length must equal scenarioCount * ceil(legs.Length / 8).
    /// </summary>
    [Required]
    public string PackedRowsBase64 { get; set; } = default!;

    [Required]
    [Range(1, int.MaxValue)]
    public int ScenarioCount { get; set; }

    public string? EventId { get; set; }
    public string? ModelVersion { get; set; }
    public bool Activate { get; set; } = true;
}
