namespace AuditaPC.Core.Model;

/// <summary>Manifiesto de una auditoría independiente.</summary>
public sealed class AuditManifest
{
    public string SchemaVersion { get; set; } = AppInfo.SchemaVersion;
    public int SchemaNumber { get; set; } = AppInfo.SchemaNumber;
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string CompanyName { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public string? Technician { get; set; }
    public string? Notes { get; set; }

    // Registro manual de la visita, separado de las consultas automáticas.
    public string? VisitSummary { get; set; }
    public string? ConnectivityNotes { get; set; }
    public string? ModulesNotes { get; set; }
    public string? ConditionNotes { get; set; }
    public string? FindingsNotes { get; set; }
    public string? ActionPlanNotes { get; set; }
    public string? LimitationsNotes { get; set; }
    public string? OtherNotes { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.Now;
    public string AppVersion { get; set; } = AppInfo.Version;
}

/// <summary>Elemento del catálogo de auditorías guardadas.</summary>
public sealed class AuditCatalogItem
{
    public string FolderPath { get; set; } = string.Empty;
    public string FolderName { get; set; } = string.Empty;
    public AuditManifest? Manifest { get; set; }
    public int? ComputerCount { get; set; }
    public string ComputerCountText => ComputerCount is int count
        ? count == 1 ? "1 equipo" : $"{count} equipos"
        : "—";
    public string? Error { get; set; }
    public DateTimeOffset? LastWriteTime { get; set; }
    public bool IsValid => Manifest is not null && Error is null;
}
