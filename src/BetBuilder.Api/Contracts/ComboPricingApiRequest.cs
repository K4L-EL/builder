using System.ComponentModel.DataAnnotations;

namespace BetBuilder.Api.Contracts;

public sealed class ComboPricingApiRequest
{
    public string? EventId { get; set; }
    public string? SnapshotId { get; set; }

    [Required]
    [MinLength(1, ErrorMessage = "At least one leg is required.")]
    public string[] Legs { get; set; } = Array.Empty<string>();
}
