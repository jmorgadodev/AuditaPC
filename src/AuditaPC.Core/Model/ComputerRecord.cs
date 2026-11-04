namespace AuditaPC.Core.Model;

/// <summary>Referencia verificable a una captura guardada en la auditoría.</summary>
public sealed class CaptureReference
{
    public string Id { get; set; } = string.Empty;
    public DateTimeOffset CapturedAt { get; set; }
    public string File { get; set; } = string.Empty;
    public string Sha256 { get; set; } = string.Empty;
    public string AppVersion { get; set; } = AppInfo.Version;
}

/// <summary>Registro persistente de un computador dentro de una auditoría.</summary>
public sealed class ComputerRecord
{
    public string SchemaVersion { get; set; } = AppInfo.SchemaVersion;
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string DisplayName { get; set; } = "Equipo sin nombre";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.Now;
    public string? CurrentCaptureId { get; set; }
    public List<CaptureReference> Captures { get; set; } = new();
    public ManualRecord Manual { get; set; } = new();
    public List<PhotoAttachment> Photos { get; set; } = new();
    public DeviceFingerprint Fingerprint { get; set; } = new();

    public CaptureReference? CurrentCapture =>
        CurrentCaptureId is null
            ? Captures.OrderByDescending(c => c.CapturedAt).FirstOrDefault()
            : Captures.FirstOrDefault(c => c.Id == CurrentCaptureId)
              ?? Captures.OrderByDescending(c => c.CapturedAt).FirstOrDefault();
}

public sealed class CaptureRecord
{
    public string SchemaVersion { get; set; } = AppInfo.SchemaVersion;
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string ComputerId { get; set; } = string.Empty;
    public DateTimeOffset CapturedAt { get; set; } = DateTimeOffset.Now;
    public string AppVersion { get; set; } = AppInfo.Version;
    public string? Note { get; set; }
    public InventorySnapshot Inventory { get; set; } = new();
}

public sealed class PhotoAttachment
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string OriginalFileName { get; set; } = string.Empty;
    public string StoredPath { get; set; } = string.Empty;
    public string DisplayPath { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/octet-stream";
    public long SizeBytes { get; set; }
    public string Sha256 { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTimeOffset AddedAt { get; set; } = DateTimeOffset.Now;
    public int? Width { get; set; }
    public int? Height { get; set; }
}
