namespace AuditaPC.Core.Model;

/// <summary>
/// Huella usada para reconocer equipos. Ningún campo por sí solo es suficiente:
/// los números de serie genéricos y los equipos renombrados son habituales.
/// </summary>
public sealed class DeviceFingerprint
{
    public string? ComputerName { get; set; }
    public string? Manufacturer { get; set; }
    public string? Model { get; set; }
    public string? BiosSerial { get; set; }
    public string? SystemUuid { get; set; }
    public string? BaseboardSerial { get; set; }
    public string? MachineGuid { get; set; }
    public string? BiosAssetTag { get; set; }
    public string? AssetCode { get; set; }
    public string? Domain { get; set; }
    public List<string> DiskSerials { get; set; } = new();

    public DeviceFingerprint Clone() => new()
    {
        ComputerName = ComputerName,
        Manufacturer = Manufacturer,
        Model = Model,
        BiosSerial = BiosSerial,
        SystemUuid = SystemUuid,
        BaseboardSerial = BaseboardSerial,
        MachineGuid = MachineGuid,
        BiosAssetTag = BiosAssetTag,
        AssetCode = AssetCode,
        Domain = Domain,
        DiskSerials = new List<string>(DiskSerials)
    };
}
