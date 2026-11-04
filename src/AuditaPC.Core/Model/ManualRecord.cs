namespace AuditaPC.Core.Model;

/// <summary>
/// Información escrita por el técnico. Se guarda separada de los datos
/// automáticos para que nunca se confunda con una consulta del sistema.
/// </summary>
public sealed class ManualRecord
{
    public string? Technician { get; set; }
    public string? AssetCode { get; set; }
    public string? Responsible { get; set; }
    public string? Location { get; set; }
    public string? Notes { get; set; }
    public string? IssuesFound { get; set; }
    public string? Interventions { get; set; }
    public string? PendingWork { get; set; }
    public string? Recommendations { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }

    public bool IsEmpty =>
        string.IsNullOrWhiteSpace(Technician) && string.IsNullOrWhiteSpace(AssetCode)
        && string.IsNullOrWhiteSpace(Responsible) && string.IsNullOrWhiteSpace(Location)
        && string.IsNullOrWhiteSpace(Notes) && string.IsNullOrWhiteSpace(IssuesFound)
        && string.IsNullOrWhiteSpace(Interventions) && string.IsNullOrWhiteSpace(PendingWork)
        && string.IsNullOrWhiteSpace(Recommendations);
}
