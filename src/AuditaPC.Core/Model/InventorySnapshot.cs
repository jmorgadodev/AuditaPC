namespace AuditaPC.Core.Model;

/// <summary>Contexto en el que se ejecutó la captura. No contiene credenciales.</summary>
public sealed class CaptureEnvironment
{
    public string AppVersion { get; set; } = AppInfo.Version;
    public string MachineName { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string UserDomain { get; set; } = string.Empty;
    public bool IsElevated { get; set; }
    public bool OnlineLicenseChecksEnabled { get; set; }
    public bool Is64BitOperatingSystem { get; set; }
    public bool Is64BitProcess { get; set; }
    public string? OsDescription { get; set; }
    public string? ProcessArchitecture { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset FinishedAt { get; set; }
}

/// <summary>Instantánea completa del inventario automático de un equipo.</summary>
public sealed class InventorySnapshot
{
    public string SchemaVersion { get; set; } = AppInfo.SchemaVersion;
    public CaptureEnvironment Environment { get; set; } = new();
    public CategoryResult<IdentityData> Identity { get; set; } = new() { Id = "identity", Title = "Identificación" };
    public CategoryResult<OsData> OperatingSystem { get; set; } = new() { Id = "os", Title = "Sistema operativo" };
    public CategoryResult<HardwareData> Hardware { get; set; } = new() { Id = "hardware", Title = "Procesador y memoria" };
    public CategoryResult<StorageData> Storage { get; set; } = new() { Id = "storage", Title = "Almacenamiento" };
    public CategoryResult<NetworkData> Network { get; set; } = new() { Id = "network", Title = "Red local" };
    public CategoryResult<SoftwareData> Software { get; set; } = new() { Id = "software", Title = "Software instalado" };
    public CategoryResult<OfficeData> Office { get; set; } = new() { Id = "office", Title = "Microsoft Office" };
    public CategoryResult<SecurityData> Security { get; set; } = new() { Id = "security", Title = "Seguridad de Windows" };

    public IEnumerable<ICategoryResult> Categories()
    {
        yield return Identity;
        yield return OperatingSystem;
        yield return Hardware;
        yield return Storage;
        yield return Network;
        yield return Software;
        yield return Office;
        yield return Security;
    }

    public IEnumerable<CheckRecord> AllChecks() =>
        Categories().SelectMany(c => c.Checks);
}
