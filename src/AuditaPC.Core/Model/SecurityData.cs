namespace AuditaPC.Core.Model;

public sealed class SecurityData
{
    public List<AntivirusInfo> Antivirus { get; set; } = new();
    public List<FirewallProfileInfo> Firewall { get; set; } = new();
    public List<BitLockerVolumeInfo> BitLocker { get; set; } = new();
    public TpmInfo? Tpm { get; set; }
    public SecureBootInfo? SecureBoot { get; set; }
    public DefenderStatusInfo? Defender { get; set; }
}

public sealed class AntivirusInfo
{
    public string? DisplayName { get; set; }
    public int? ProductStateRaw { get; set; }
    public bool? Enabled { get; set; }
    public bool? DefinitionsUpToDate { get; set; }
    public string? Provider { get; set; }
    public string? Source { get; set; }
    public string? Note { get; set; }
}

public sealed class FirewallProfileInfo
{
    public string? Profile { get; set; }
    public bool? Enabled { get; set; }
    public string? Source { get; set; }
}

public sealed class BitLockerVolumeInfo
{
    public string? DriveLetter { get; set; }
    public int? ProtectionStatus { get; set; }
    public int? ConversionStatus { get; set; }
    public string? ProtectionText { get; set; }
    public string? ConversionText { get; set; }
    public string? EncryptionMethod { get; set; }
    public string? Source { get; set; }
}

public sealed class TpmInfo
{
    public bool? Present { get; set; }
    public bool? Ready { get; set; }
    public bool? Enabled { get; set; }
    public bool? Activated { get; set; }
    public string? SpecVersion { get; set; }
    public string? Manufacturer { get; set; }
    public string? Source { get; set; }
}

public sealed class SecureBootInfo
{
    public bool? Enabled { get; set; }
    public string? FirmwareType { get; set; }
    public string? Source { get; set; }
}

public sealed class DefenderStatusInfo
{
    public bool? AntivirusEnabled { get; set; }
    public bool? RealTimeProtectionEnabled { get; set; }
    public string? SignatureVersion { get; set; }
    public int? SignatureAgeDays { get; set; }
    public bool? TamperProtectionEnabled { get; set; }
    public string? Source { get; set; }
}
