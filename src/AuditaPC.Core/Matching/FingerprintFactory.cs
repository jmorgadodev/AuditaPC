using AuditaPC.Core.Model;

namespace AuditaPC.Core.Matching;

public static class FingerprintFactory
{
    public static DeviceFingerprint FromSnapshot(InventorySnapshot snapshot, string? assetCode = null)
    {
        var identity = snapshot.Identity.Data;
        var storage = snapshot.Storage.Data;

        return new DeviceFingerprint
        {
            ComputerName = Clean(identity?.ComputerName),
            Manufacturer = Clean(identity?.Manufacturer),
            Model = Clean(identity?.Model),
            BiosSerial = Clean(identity?.BiosSerial),
            SystemUuid = Clean(identity?.SystemUuid),
            BaseboardSerial = Clean(identity?.BaseboardSerial),
            MachineGuid = Clean(identity?.MachineGuid),
            BiosAssetTag = Clean(identity?.BiosAssetTag),
            AssetCode = Clean(assetCode),
            Domain = Clean(identity?.Domain),
            DiskSerials = storage?.PhysicalDisks
                .Select(d => Clean(d.SerialNumber))
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList() ?? new List<string>()
        };
    }

    public static DeviceFingerprint Merge(DeviceFingerprint previous, DeviceFingerprint incoming)
    {
        return new DeviceFingerprint
        {
            ComputerName = First(incoming.ComputerName, previous.ComputerName),
            Manufacturer = First(incoming.Manufacturer, previous.Manufacturer),
            Model = First(incoming.Model, previous.Model),
            BiosSerial = First(incoming.BiosSerial, previous.BiosSerial),
            SystemUuid = First(incoming.SystemUuid, previous.SystemUuid),
            BaseboardSerial = First(incoming.BaseboardSerial, previous.BaseboardSerial),
            MachineGuid = First(incoming.MachineGuid, previous.MachineGuid),
            BiosAssetTag = First(incoming.BiosAssetTag, previous.BiosAssetTag),
            AssetCode = First(incoming.AssetCode, previous.AssetCode),
            Domain = First(incoming.Domain, previous.Domain),
            DiskSerials = previous.DiskSerials
                .Concat(incoming.DiskSerials)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList()
        };
    }

    public static string GetDisplayName(InventorySnapshot snapshot, string? fallback = null)
    {
        var identity = snapshot.Identity.Data;
        if (!string.IsNullOrWhiteSpace(identity?.ComputerName))
        {
            return identity.ComputerName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(identity?.BiosSerial))
        {
            return "Equipo " + identity.BiosSerial.Trim();
        }

        return string.IsNullOrWhiteSpace(fallback) ? "Equipo sin nombre" : fallback;
    }

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? First(string? primary, string? secondary) =>
        !string.IsNullOrWhiteSpace(primary) ? primary : secondary;
}
