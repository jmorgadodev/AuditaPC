using System.Runtime.InteropServices;

namespace AuditaPC.Core.Inventory.Collectors;

public static class FirmwareInfo
{
    private enum FirmwareType : uint
    {
        Unknown = 0,
        Bios = 1,
        Uefi = 2,
        Max = 3
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFirmwareType(out FirmwareType firmwareType);

    public static string? GetFirmwareType()
    {
        try
        {
            if (GetFirmwareType(out var type))
            {
                return type switch
                {
                    FirmwareType.Bios => "BIOS (heredado)",
                    FirmwareType.Uefi => "UEFI",
                    _ => "Desconocido"
                };
            }
        }
        catch
        {
            // Se informa como no verificable.
        }

        return null;
    }
}
