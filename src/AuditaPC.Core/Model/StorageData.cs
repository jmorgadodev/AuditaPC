namespace AuditaPC.Core.Model;

public sealed class StorageData
{
    public List<PhysicalDiskInfo> PhysicalDisks { get; set; } = new();
    public List<VolumeInfo> Volumes { get; set; } = new();
    public bool? HealthQueryAvailable { get; set; }
    public string? HealthQueryNote { get; set; }
}

public sealed class PhysicalDiskInfo
{
    public string? Model { get; set; }
    public string? SerialNumber { get; set; }
    public long? SizeBytes { get; set; }
    public string? InterfaceType { get; set; }
    public string? MediaType { get; set; }
    public string? BusType { get; set; }
    public string? FirmwareVersion { get; set; }
    public string? HealthStatus { get; set; }
    public string? OperationalStatus { get; set; }
    public string? HealthSource { get; set; }
    public int? DiskIndex { get; set; }
}

public sealed class VolumeInfo
{
    public string? DriveLetter { get; set; }
    public string? Label { get; set; }
    public string? FileSystem { get; set; }
    public long? SizeBytes { get; set; }
    public long? FreeBytes { get; set; }
    public double? UsedPercent { get; set; }
    public bool? IsSystemVolume { get; set; }
    public string? DriveType { get; set; }
}
