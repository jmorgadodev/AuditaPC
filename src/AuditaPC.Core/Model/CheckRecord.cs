namespace AuditaPC.Core.Model;

/// <summary>Una comprobación concreta dentro de una categoría del inventario.</summary>
public sealed class CheckRecord
{
    public string Id { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public CheckStatus Status { get; set; } = CheckStatus.NotVerifiable;
    public string? Detail { get; set; }
    public string? Source { get; set; }
    public string? ErrorType { get; set; }

    public static CheckRecord Verified(string id, string label, string? detail = null, string? source = null) =>
        new() { Id = id, Label = label, Status = CheckStatus.Verified, Detail = detail, Source = source };

    public static CheckRecord Detected(string id, string label, string? detail = null, string? source = null) =>
        new() { Id = id, Label = label, Status = CheckStatus.Detected, Detail = detail, Source = source };

    public static CheckRecord NotDetected(string id, string label, string? detail = null, string? source = null) =>
        new() { Id = id, Label = label, Status = CheckStatus.NotDetected, Detail = detail, Source = source };

    public static CheckRecord Problem(string id, string label, string? detail = null, string? source = null) =>
        new() { Id = id, Label = label, Status = CheckStatus.Problem, Detail = detail, Source = source };

    public static CheckRecord NotVerifiable(string id, string label, string? detail = null, string? source = null) =>
        new() { Id = id, Label = label, Status = CheckStatus.NotVerifiable, Detail = detail, Source = source };

    public static CheckRecord RequiresElevation(string id, string label, string? detail = null, string? source = null) =>
        new() { Id = id, Label = label, Status = CheckStatus.RequiresElevation, Detail = detail, Source = source };

    public static CheckRecord PermissionDenied(string id, string label, string? detail = null, string? source = null) =>
        new() { Id = id, Label = label, Status = CheckStatus.PermissionDenied, Detail = detail, Source = source };

    public static CheckRecord NotTestedOffline(string id, string label, string? detail = null, string? source = null) =>
        new() { Id = id, Label = label, Status = CheckStatus.NotTestedOffline, Detail = detail, Source = source };

    public static CheckRecord KnownLimitation(string id, string label, string? detail = null, string? source = null) =>
        new() { Id = id, Label = label, Status = CheckStatus.KnownLimitation, Detail = detail, Source = source };

    public static CheckRecord NotApplicable(string id, string label, string? detail = null, string? source = null) =>
        new() { Id = id, Label = label, Status = CheckStatus.NotApplicable, Detail = detail, Source = source };

    public static CheckRecord Error(string id, string label, string? detail = null, string? errorType = null, string? source = null) =>
        new() { Id = id, Label = label, Status = CheckStatus.QueryError, Detail = detail, ErrorType = errorType, Source = source };

    public static CheckRecord Timeout(string id, string label, string? detail = null, string? source = null) =>
        new() { Id = id, Label = label, Status = CheckStatus.Timeout, Detail = detail, Source = source };
}
