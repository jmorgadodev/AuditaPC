namespace AuditaPC.Core.Model;

public sealed class OfficeData
{
    public bool? Installed { get; set; }
    public List<OfficeInstallationInfo> Installations { get; set; } = new();
    public List<OfficeLicenseInfo> Licenses { get; set; } = new();
    public string? SubscriptionNote { get; set; }
    public string? DetectionNote { get; set; }
}

public sealed class OfficeInstallationInfo
{
    public string? ProductName { get; set; }
    public string? ReleaseId { get; set; }
    public string? Version { get; set; }
    public string? Build { get; set; }
    public string? Architecture { get; set; }
    public string? UpdateChannel { get; set; }
    public string? ChannelId { get; set; }
    public string? InstallType { get; set; }
    public string? InstallPath { get; set; }
    public string? DetectedBy { get; set; }
}

public sealed class OfficeLicenseInfo
{
    public string? ProductName { get; set; }
    public string? Description { get; set; }
    public int? LicenseStatus { get; set; }
    public string? LicenseStatusText { get; set; }
    public bool? IsLicensed { get; set; }
    public int? GracePeriodRemainingMinutes { get; set; }
    public string? Source { get; set; }
    public string? QueryNote { get; set; }
}
