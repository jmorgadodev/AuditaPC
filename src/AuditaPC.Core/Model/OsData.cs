namespace AuditaPC.Core.Model;

public sealed class OsData
{
    public string? ProductName { get; set; }
    public string? Edition { get; set; }
    public string? DisplayVersion { get; set; }
    public string? Version { get; set; }
    public string? BuildNumber { get; set; }
    public int? UpdateBuildRevision { get; set; }
    public string? Architecture { get; set; }
    public DateTimeOffset? InstallDate { get; set; }
    public string? InstallDateSource { get; set; }
    public DateTimeOffset? LastBootTime { get; set; }
    public string? InstallationType { get; set; }
    public string? SystemDrive { get; set; }
    public string? SystemLocale { get; set; }
    public WindowsLicenseInfo License { get; set; } = new();
    public List<WindowsUpdateInfo> RecentUpdates { get; set; } = new();
    public string? FriendlyName { get; set; }
}

public sealed class WindowsLicenseInfo
{
    public string? ProductName { get; set; }
    public string? Description { get; set; }
    public int? LicenseStatus { get; set; }
    public string? LicenseStatusText { get; set; }
    public bool? IsLicensed { get; set; }
    public int? GracePeriodRemainingMinutes { get; set; }
    public string? Channel { get; set; }
    public string? Source { get; set; }
    public string? QueryNote { get; set; }
}

public sealed class WindowsUpdateInfo
{
    public string? HotFixId { get; set; }
    public string? Description { get; set; }
    public DateTimeOffset? InstalledOn { get; set; }
    public string? InstalledBy { get; set; }
}
