namespace AuditaPC.Core.Model;

/// <summary>Identificación general del equipo y contexto de dominio.</summary>
public sealed class IdentityData
{
    public string? ComputerName { get; set; }
    public string? Manufacturer { get; set; }
    public string? Model { get; set; }
    public string? SystemType { get; set; }
    public string? ChassisType { get; set; }
    public string? Architecture { get; set; }
    public string? BiosVendor { get; set; }
    public string? BiosVersion { get; set; }
    public DateTimeOffset? BiosReleaseDate { get; set; }
    public string? BiosSerial { get; set; }
    public string? BiosAssetTag { get; set; }
    public string? SystemUuid { get; set; }
    public string? BaseboardManufacturer { get; set; }
    public string? BaseboardProduct { get; set; }
    public string? BaseboardSerial { get; set; }
    public string? MachineGuid { get; set; }
    public string? FirmwareType { get; set; }
    public string? Domain { get; set; }
    public string? Workgroup { get; set; }
    public bool? PartOfDomain { get; set; }
    public string? DomainRole { get; set; }
    public bool? EntraJoined { get; set; }
    public bool? EntraEnterpriseJoined { get; set; }
    public string? EntraTenantName { get; set; }
    public string? CurrentUser { get; set; }
    public string? CurrentUserDomain { get; set; }
    public bool? IsElevated { get; set; }
    public bool? HypervisorPresent { get; set; }
    public bool? IsVirtualMachine { get; set; }
    public string? VirtualizationHint { get; set; }
}
