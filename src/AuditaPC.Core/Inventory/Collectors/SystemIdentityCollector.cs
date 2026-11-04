using System.Runtime.InteropServices;
using AuditaPC.Core.Matching;
using AuditaPC.Core.Model;
using Microsoft.Win32;

namespace AuditaPC.Core.Inventory.Collectors;

public sealed class SystemIdentityCollector : IInventoryCollector
{
    public string Id => "identity";
    public string Title => "Identificación";
    public TimeSpan Timeout => TimeSpan.FromSeconds(16);

    public async Task<CollectorOutcome> CollectAsync(CollectorContext context)
    {
        var checks = new List<CheckRecord>();
        var data = new IdentityData
        {
            CurrentUser = context.UserName,
            CurrentUserDomain = context.UserDomain,
            IsElevated = context.IsElevated,
            Architecture = RuntimeInformation.OSArchitecture.ToString()
        };

        var deviceObtained = false;

        var (computerRows, computerError) = TryQuery("Win32_ComputerSystem", TimeSpan.FromSeconds(7));
        if (computerRows.Count > 0)
        {
            var row = computerRows[0];
            data.ComputerName = Clean(row.GetString("Name"));
            data.Manufacturer = Clean(row.GetString("Manufacturer"));
            data.Model = Clean(row.GetString("Model"));
            data.SystemType = Clean(row.GetString("SystemType"));
            data.Domain = Clean(row.GetString("Domain"));
            data.Workgroup = Clean(row.GetString("Workgroup"));
            data.PartOfDomain = row.GetBool("PartOfDomain");
            data.HypervisorPresent = row.GetBool("HypervisorPresent");
            data.DomainRole = MapDomainRole(row.GetInt("DomainRole"));
            deviceObtained = true;
            checks.Add(CheckRecord.Verified(
                "identity.system",
                "Identificación del equipo",
                $"{data.Manufacturer} {data.Model}".Trim(),
                "WMI Win32_ComputerSystem"));
        }
        else
        {
            checks.Add(computerError is null
                ? CheckRecord.NotVerifiable("identity.system", "Identificación del equipo",
                    "Windows no devolvió datos de Win32_ComputerSystem.", "WMI Win32_ComputerSystem")
                : CollectorErrors.FromWmi("identity.system", "Identificación del equipo", computerError));
        }

        var (biosRows, biosError) = TryQuery("Win32_BIOS", TimeSpan.FromSeconds(6));
        if (biosRows.Count > 0)
        {
            var row = biosRows[0];
            data.BiosVendor = Clean(row.GetString("Manufacturer"));
            data.BiosVersion = Clean(row.GetString("SMBIOSBIOSVersion"));
            data.BiosReleaseDate = row.GetDateTime("ReleaseDate");
            data.BiosSerial = Clean(row.GetString("SerialNumber"));
            checks.Add(MatchNormalization.IsUsable(data.BiosSerial)
                ? CheckRecord.Verified("identity.bios.serial", "Número de serie del BIOS",
                    data.BiosSerial, "WMI Win32_BIOS")
                : CheckRecord.NotVerifiable("identity.bios.serial", "Número de serie del BIOS",
                    "El BIOS no expone un número de serie utilizable (valor ausente o genérico).",
                    "WMI Win32_BIOS"));
        }
        else
        {
            checks.Add(biosError is null
                ? CheckRecord.NotVerifiable("identity.bios.serial", "Número de serie del BIOS",
                    "Windows no devolvió datos de Win32_BIOS.", "WMI Win32_BIOS")
                : CollectorErrors.FromWmi("identity.bios.serial", "Número de serie del BIOS", biosError));
        }

        var (productRows, productError) = TryQuery("Win32_ComputerSystemProduct", TimeSpan.FromSeconds(6));
        if (productRows.Count > 0)
        {
            var row = productRows[0];
            data.SystemUuid = Clean(row.GetString("UUID"));
            if (string.IsNullOrWhiteSpace(data.BiosSerial))
            {
                data.BiosSerial = Clean(row.GetString("IdentifyingNumber"));
            }

            checks.Add(MatchNormalization.IsUsable(data.SystemUuid)
                ? CheckRecord.Verified("identity.uuid", "UUID SMBIOS",
                    data.SystemUuid, "WMI Win32_ComputerSystemProduct")
                : CheckRecord.NotVerifiable("identity.uuid", "UUID SMBIOS",
                    "El firmware expone un UUID ausente o genérico.",
                    "WMI Win32_ComputerSystemProduct"));
        }
        else
        {
            checks.Add(productError is null
                ? CheckRecord.NotVerifiable("identity.uuid", "UUID SMBIOS",
                    "Windows no devolvió datos de Win32_ComputerSystemProduct.",
                    "WMI Win32_ComputerSystemProduct")
                : CollectorErrors.FromWmi("identity.uuid", "UUID SMBIOS", productError));
        }

        var (boardRows, boardError) = TryQuery("Win32_BaseBoard", TimeSpan.FromSeconds(6));
        if (boardRows.Count > 0)
        {
            var row = boardRows[0];
            data.BaseboardManufacturer = Clean(row.GetString("Manufacturer"));
            data.BaseboardProduct = Clean(row.GetString("Product"));
            data.BaseboardSerial = Clean(row.GetString("SerialNumber"));
            checks.Add(MatchNormalization.IsUsable(data.BaseboardSerial)
                ? CheckRecord.Verified("identity.board", "Placa base",
                    $"{data.BaseboardManufacturer} {data.BaseboardProduct}".Trim(),
                    "WMI Win32_BaseBoard")
                : CheckRecord.Detected("identity.board", "Placa base",
                    $"{data.BaseboardManufacturer} {data.BaseboardProduct}".Trim() +
                    " (sin número de serie utilizable)", "WMI Win32_BaseBoard"));
        }
        else if (boardError is not null)
        {
            checks.Add(CollectorErrors.FromWmi("identity.board", "Placa base", boardError));
        }

        var (enclosureRows, enclosureError) = TryQuery("Win32_SystemEnclosure", TimeSpan.FromSeconds(6));
        if (enclosureRows.Count > 0)
        {
            var row = enclosureRows[0];
            data.ChassisType = MapChassis(row.GetIntList("ChassisTypes"));
            data.BiosAssetTag = Clean(row.GetString("SMBIOSAssetTag"));
            checks.Add(CheckRecord.Verified("identity.chassis", "Tipo de equipo",
                data.ChassisType, "WMI Win32_SystemEnclosure"));
        }
        else if (enclosureError is not null)
        {
            checks.Add(CollectorErrors.FromWmi("identity.chassis", "Tipo de equipo", enclosureError));
        }

        data.MachineGuid = ReadMachineGuid(checks);
        data.FirmwareType = FirmwareInfo.GetFirmwareType();
        checks.Add(data.FirmwareType is null
            ? CheckRecord.NotVerifiable("identity.firmware", "Tipo de firmware",
                "Windows no pudo determinar si el equipo usa BIOS o UEFI.", "Kernel32 GetFirmwareType")
            : CheckRecord.Verified("identity.firmware", "Tipo de firmware",
                data.FirmwareType, "Kernel32 GetFirmwareType"));

        if (data.PartOfDomain is not null)
        {
            var detail = data.PartOfDomain == true
                ? $"Dominio: {Formatting.OrDash(data.Domain)} ({data.DomainRole ?? "rol no determinado"})"
                : $"Grupo de trabajo: {Formatting.OrDash(data.Workgroup)}";
            checks.Add(CheckRecord.Verified("identity.domain", "Dominio o grupo de trabajo",
                detail, "WMI Win32_ComputerSystem"));
        }
        else
        {
            checks.Add(CheckRecord.NotVerifiable("identity.domain", "Dominio o grupo de trabajo",
                "Windows no informó la pertenencia a dominio o grupo de trabajo.",
                "WMI Win32_ComputerSystem"));
        }

        var entraCheck = await ReadEntraAsync(context, data).ConfigureAwait(false);
        checks.Add(entraCheck);

        ApplyVirtualizationHint(data);

        checks.Add(CheckRecord.Verified(
            "identity.context",
            "Contexto de ejecución",
            $"Usuario: {context.UserName}; elevación: {(context.IsElevated ? "sí" : "no")}",
            "WindowsIdentity"));

        var result = new CategoryResult<IdentityData>
        {
            Id = Id,
            Title = Title,
            Data = data,
            Checks = checks,
            Status = CategoryStatus.Aggregate(checks, deviceObtained)
        };

        return new CollectorOutcome
        {
            Id = Id,
            Status = result.Status,
            Apply = snapshot => snapshot.Identity = result,
            Message = deviceObtained ? null : "No se pudo obtener la identificación básica del equipo."
        };
    }

    public void ApplyUnavailable(InventorySnapshot target, CheckStatus status, string message, string? errorType)
    {
        target.Identity = CategoryResult<IdentityData>.Unavailable(
            Id, Title, status, message, errorType);
    }

    private static (IReadOnlyList<WmiRow> Rows, WmiQueryException? Error) TryQuery(
        string className,
        TimeSpan timeout)
    {
        try
        {
            return (WmiQuery.Query(className, timeout: timeout), null);
        }
        catch (WmiQueryException ex)
        {
            return (Array.Empty<WmiRow>(), ex);
        }
    }

    private static string? ReadMachineGuid(List<CheckRecord> checks)
    {
        try
        {
            using var key = RegistryHelper.OpenKey(
                RegistryHive.LocalMachine, @"SOFTWARE\Microsoft\Cryptography", RegistryView.Registry64);
            var guid = Clean(RegistryHelper.GetString(key, "MachineGuid"));
            checks.Add(string.IsNullOrWhiteSpace(guid)
                ? CheckRecord.NotVerifiable("identity.machineguid", "GUID de instalación de Windows",
                    "El Registro no contiene MachineGuid.", "Registro de Windows")
                : CheckRecord.Verified("identity.machineguid", "GUID de instalación de Windows",
                    guid, "Registro de Windows"));
            return guid;
        }
        catch (RegistryReadException ex)
        {
            checks.Add(CollectorErrors.FromRegistry(
                "identity.machineguid", "GUID de instalación de Windows", ex));
            return null;
        }
    }

    private static async Task<CheckRecord> ReadEntraAsync(CollectorContext context, IdentityData data)
    {
        var executable = Path.Combine(Environment.SystemDirectory, "dsregcmd.exe");
        if (!File.Exists(executable))
        {
            return CheckRecord.NotVerifiable("identity.entra", "Unión a Microsoft Entra ID",
                "dsregcmd.exe no está disponible en este equipo.", "dsregcmd");
        }

        ProcessResult result;
        try
        {
            result = await ProcessRunner.RunAsync(
                executable, "/status", TimeSpan.FromSeconds(6), context.CancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return CollectorErrors.FromException("identity.entra", "Unión a Microsoft Entra ID", ex, "dsregcmd");
        }

        if (!result.Started)
        {
            return CheckRecord.NotVerifiable("identity.entra", "Unión a Microsoft Entra ID",
                $"No se pudo ejecutar dsregcmd: {result.Error}", "dsregcmd");
        }

        if (result.TimedOut)
        {
            return CheckRecord.Timeout("identity.entra", "Unión a Microsoft Entra ID",
                "dsregcmd no respondió dentro del tiempo máximo.", "dsregcmd");
        }

        if (result.ExitCode != 0)
        {
            return CheckRecord.NotVerifiable("identity.entra", "Unión a Microsoft Entra ID",
                $"dsregcmd terminó con código {result.ExitCode}. {TrimForDetail(result.StandardError)}",
                "dsregcmd");
        }

        var values = ParseKeyValues(result.StandardOutput);
        if (!values.TryGetValue("AzureAdJoined", out var joinedText))
        {
            return CheckRecord.NotVerifiable("identity.entra", "Unión a Microsoft Entra ID",
                "La salida de dsregcmd no contiene el estado AzureAdJoined.", "dsregcmd");
        }

        data.EntraJoined = IsYes(joinedText);
        if (values.TryGetValue("EnterpriseJoined", out var enterpriseText))
        {
            data.EntraEnterpriseJoined = IsYes(enterpriseText);
        }

        if (data.EntraJoined == true)
        {
            data.EntraTenantName = values.GetValueOrDefault("TenantName");
        }

        if (values.TryGetValue("DomainJoined", out var domainText))
        {
            var domainJoined = IsYes(domainText);
            var detail = data.EntraJoined == true
                ? $"Unido a Entra ID{(string.IsNullOrWhiteSpace(data.EntraTenantName) ? string.Empty : $" (inquilino: {data.EntraTenantName})")}"
                : domainJoined
                    ? "Unido a un dominio local; no unido a Entra ID"
                    : "No unido a Entra ID ni a un dominio local";
            return CheckRecord.Verified("identity.entra", "Unión a Microsoft Entra ID", detail, "dsregcmd");
        }

        return data.EntraJoined == true
            ? CheckRecord.Verified("identity.entra", "Unión a Microsoft Entra ID",
                "Unido a Entra ID", "dsregcmd")
            : CheckRecord.Detected("identity.entra", "Unión a Microsoft Entra ID",
                "dsregcmd informa que el equipo no está unido a Entra ID.", "dsregcmd");
    }

    private static Dictionary<string, string> ParseKeyValues(string output)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rawLine in output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = rawLine.IndexOf(':');
            if (separator <= 0)
            {
                continue;
            }

            var key = rawLine[..separator].Trim();
            var value = rawLine[(separator + 1)..].Trim();
            if (key.Length > 0 && !values.ContainsKey(key))
            {
                values[key] = value;
            }
        }

        return values;
    }

    private static bool IsYes(string value) =>
        value.Trim().Equals("YES", StringComparison.OrdinalIgnoreCase);

    private static string TrimForDetail(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim().Replace("\r", " ").Replace("\n", " ");
        return trimmed.Length > 200 ? trimmed[..200] + "…" : trimmed;
    }

    private static void ApplyVirtualizationHint(IdentityData data)
    {
        var haystack = $"{data.Manufacturer} {data.Model} {data.BiosVendor}".ToUpperInvariant();
        string[] markers =
        {
            "VMWARE", "VIRTUALBOX", "INNOTEK", "QEMU", "XEN", "PARALLELS",
            "VIRTUAL MACHINE", "HYPER-V", "BOCHS", "KVM", "VIRTUALBOX"
        };

        if (markers.Any(marker => haystack.Contains(marker, StringComparison.Ordinal)))
        {
            data.IsVirtualMachine = true;
            data.VirtualizationHint = "El fabricante o modelo indica una máquina virtual.";
            return;
        }

        if (data.HypervisorPresent == true)
        {
            data.IsVirtualMachine = null;
            data.VirtualizationHint =
                "Hay un hipervisor presente. Puede ser un equipo físico con Hyper-V o una máquina virtual.";
        }
    }

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string MapDomainRole(int? role) => role switch
    {
        0 => "Estación de trabajo independiente",
        1 => "Miembro de dominio",
        2 => "Servidor independiente",
        3 => "Servidor miembro",
        4 => "Controlador de dominio de respaldo",
        5 => "Controlador de dominio principal",
        _ => "No determinado"
    };

    private static string MapChassis(List<int> codes)
    {
        if (codes.Count == 0)
        {
            return "No determinado";
        }

        if (codes.Any(code => code is 30 or 31 or 32))
        {
            return "Tablet / convertible";
        }

        if (codes.Any(code => code is 8 or 9 or 10 or 11 or 14))
        {
            return "Portátil";
        }

        if (codes.Contains(12))
        {
            return "Estación de acoplamiento";
        }

        if (codes.Any(code => code is 17 or 23 or 28 or 38))
        {
            return "Servidor / rack";
        }

        if (codes.Any(code => code is 3 or 4 or 5 or 6 or 7 or 13 or 15 or 16 or 24 or 35 or 36))
        {
            return "Escritorio";
        }

        if (codes.Contains(34))
        {
            return "Mini PC / stick";
        }

        return "Otro";
    }
}
