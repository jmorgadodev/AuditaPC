namespace AuditaPC.Core.Model;

public sealed class SoftwareData
{
    public List<InstalledProgramInfo> Programs { get; set; } = new();
    public int TotalCount { get; set; }
    public List<string> Limitations { get; set; } = new();
    public int? SystemScopeCount { get; set; }
    public int? UserScopeCount { get; set; }
}

public sealed class InstalledProgramInfo
{
    public string? Name { get; set; }
    public string? Version { get; set; }
    public string? Publisher { get; set; }
    public DateTimeOffset? InstallDate { get; set; }
    public string? InstallLocation { get; set; }
    public string? Scope { get; set; }
    public string? Architecture { get; set; }
    public string? RegistryView { get; set; }
    public string? ProductCode { get; set; }
}
