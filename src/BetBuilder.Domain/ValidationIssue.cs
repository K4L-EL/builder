namespace BetBuilder.Domain;

public sealed class ValidationIssue
{
    public ValidationIssueCode Code { get; init; }
    public string Message { get; init; } = default!;
    public bool IsError { get; init; }

    public static ValidationIssue Error(ValidationIssueCode code, string message) =>
        new() { Code = code, Message = message, IsError = true };

    public static ValidationIssue Warning(ValidationIssueCode code, string message) =>
        new() { Code = code, Message = message, IsError = false };
}

public enum ValidationIssueCode
{
    UnknownLeg,
    DuplicateLeg,
    UnavailableLeg,
    MutuallyExclusive,
    RedundantSelection,
    MaxLegsExceeded,
    ImpossibleCombo
}
