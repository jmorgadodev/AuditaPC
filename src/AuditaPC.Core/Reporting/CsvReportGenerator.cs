using System.Globalization;
using System.Text;
using AuditaPC.Core.Model;

namespace AuditaPC.Core.Reporting;

/// <summary>
/// Exportaciones CSV delimitadas por punto y coma y codificadas en UTF-8 con BOM,
/// pensadas para abrirse directamente en Excel en español.
/// </summary>
public static class CsvReportGenerator
{
    private const char Delimiter = ';';

    public static byte[] BuildSummary(AuditReportData data)
    {
        var builder = new StringBuilder();
        AppendRow(builder,
            "Equipo", "Fabricante", "Modelo", "Serie BIOS", "UUID SMBIOS",
            "Código de activo", "Ubicación", "Responsable",
            "Sistema operativo", "Edición", "Versión", "Compilación", "Arquitectura",
            "Procesador", "Núcleos", "Hilos", "RAM (GB)",
            "Discos", "Almacenamiento total (GB)", "Volúmenes",
            "Direcciones IPv4", "MAC", "Dominio",
            "Office instalado", "Producto Office", "Versión Office", "Tipo de licencia Office",
            "Licencia de Windows", "Estado de licencia de Windows",
            "Antivirus", "Firewall", "BitLocker", "TPM", "Secure Boot",
            "Capturas", "Última captura", "Comprobaciones con problema",
            "Comprobaciones incompletas");

        foreach (var computer in data.Computers)
        {
            var inventory = computer.Inventory;
            var identity = inventory?.Identity.Data;
            var os = inventory?.OperatingSystem.Data;
            var hardware = inventory?.Hardware.Data;
            var storage = inventory?.Storage.Data;
            var network = inventory?.Network.Data;
            var office = inventory?.Office.Data;
            var security = inventory?.Security.Data;
            var checks = computer.Checks.ToList();

            var cpu = hardware?.Processors.FirstOrDefault();
            var disks = storage?.PhysicalDisks.Count ?? 0;
            var totalDiskBytes = storage?.PhysicalDisks.Sum(disk => disk.SizeBytes ?? 0) ?? 0;
            var volumes = storage?.Volumes.Count ?? 0;
            var activeAdapter = network?.Adapters.FirstOrDefault(adapter =>
                adapter.IPv4Addresses.Count > 0);
            var officeProduct = office?.Installations.FirstOrDefault()?.ProductName;
            var officeVersion = office?.Installations
                .Select(installation => installation.Version)
                .FirstOrDefault(version => !string.IsNullOrWhiteSpace(version));
            var officeLicense = inventory?.Office.Checks
                .FirstOrDefault(check => check.Id == "office.license-type")?.Detail;
            var bitLocker = security?.BitLocker.Count > 0
                ? string.Join(" | ", security.BitLocker.Select(volume =>
                    $"{volume.DriveLetter}: {volume.ProtectionText ?? "sin datos"}"))
                : "—";

            AppendRow(builder,
                computer.DisplayName,
                computer.Manufacturer,
                computer.Model,
                computer.Serial,
                identity?.SystemUuid,
                computer.Record.Manual.AssetCode,
                computer.Record.Manual.Location,
                computer.Record.Manual.Responsible,
                os?.ProductName,
                os?.Edition,
                os?.DisplayVersion,
                os?.BuildNumber,
                os?.Architecture,
                cpu?.Name,
                cpu?.Cores?.ToString(CultureInfo.InvariantCulture),
                cpu?.LogicalProcessors?.ToString(CultureInfo.InvariantCulture),
                hardware?.TotalMemoryBytes is { } memory
                    ? (memory / (1024d * 1024 * 1024)).ToString("0.0", CultureInfo.InvariantCulture)
                    : null,
                disks.ToString(CultureInfo.InvariantCulture),
                (totalDiskBytes / (1024d * 1024 * 1024)).ToString("0.0", CultureInfo.InvariantCulture),
                volumes.ToString(CultureInfo.InvariantCulture),
                activeAdapter is null ? null : string.Join(" | ", activeAdapter.IPv4Addresses),
                activeAdapter?.MacAddress,
                identity?.Domain ?? identity?.Workgroup,
                office?.Installed switch { true => "Sí", false => "No", null => "No verificable" },
                officeProduct,
                officeVersion,
                officeLicense,
                os?.License.ProductName,
                os?.License.LicenseStatusText,
                security?.Antivirus.Count > 0
                    ? string.Join(" | ", security.Antivirus.Select(av =>
                        av.DisplayName + (av.Enabled == true ? " (activo)" : av.Enabled == false ? " (inactivo)" : "")))
                    : "—",
                security?.Firewall.Count > 0
                    ? string.Join(" | ", security.Firewall.Select(profile =>
                        $"{profile.Profile}: {(profile.Enabled == true ? "activo" : profile.Enabled == false ? "inactivo" : "sin datos")}"))
                    : "—",
                bitLocker,
                security?.Tpm is null
                    ? "No verificable"
                    : $"Presente · valor inicial habilitado: {Formatting.YesNo(security.Tpm.Enabled)} · " +
                      $"valor inicial activado: {Formatting.YesNo(security.Tpm.Activated)}",
                security?.SecureBoot is null
                    ? "No verificable"
                    : security.SecureBoot.Enabled == true ? "Activado" : "Desactivado",
                computer.Record.Captures.Count.ToString(CultureInfo.InvariantCulture),
                Formatting.Date(computer.Record.CurrentCapture?.CapturedAt),
                checks.Count(check => check.Status == CheckStatus.Problem).ToString(CultureInfo.InvariantCulture),
                checks.Count(check => check.Status.IsIncomplete()).ToString(CultureInfo.InvariantCulture));
        }

        return Encode(builder);
    }

    public static byte[] BuildSoftware(AuditReportData data)
    {
        var builder = new StringBuilder();
        AppendRow(builder,
            "Equipo", "Programa", "Versión", "Fabricante", "Instalación",
            "Ámbito", "Arquitectura", "Vista del Registro");

        foreach (var computer in data.Computers)
        {
            foreach (var program in computer.Inventory?.Software.Data?.Programs
                         .OrderBy(program => program.Name, StringComparer.CurrentCultureIgnoreCase)
                     ?? Enumerable.Empty<InstalledProgramInfo>())
            {
                AppendRow(builder,
                    computer.DisplayName,
                    program.Name,
                    program.Version,
                    program.Publisher,
                    Formatting.Date(program.InstallDate),
                    program.Scope,
                    program.Architecture,
                    program.RegistryView);
            }
        }

        return Encode(builder);
    }

    public static byte[] BuildChecks(AuditReportData data)
    {
        var builder = new StringBuilder();
        AppendRow(builder,
            "Equipo", "Categoría", "Comprobación", "Estado", "Detalle", "Fuente", "Tipo de error");

        foreach (var computer in data.Computers)
        {
            foreach (var category in computer.Inventory?.Categories() ?? Enumerable.Empty<ICategoryResult>())
            {
                foreach (var check in category.Checks)
                {
                    AppendRow(builder,
                        computer.DisplayName,
                        category.Title,
                        check.Label,
                        check.Status.ToDisplayText(),
                        check.Detail,
                        check.Source,
                        check.ErrorType);
                }
            }
        }

        return Encode(builder);
    }

    private static void AppendRow(StringBuilder builder, params string?[] values)
    {
        for (var index = 0; index < values.Length; index++)
        {
            if (index > 0)
            {
                builder.Append(Delimiter);
            }

            builder.Append(Escape(values[index]));
        }

        builder.Append("\r\n");
    }

    private static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var normalized = value
            .Replace("\r\n", " ", StringComparison.Ordinal)
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Trim();

        // Neutraliza fórmulas de Excel/LibreOffice introducidas en campos
        // manuales (por ejemplo, nombres, códigos o descripciones).
        if (normalized.Length > 0 && normalized[0] is '=' or '+' or '-' or '@')
        {
            normalized = "'" + normalized;
        }

        if (normalized.IndexOfAny(new[] { Delimiter, '"' }) >= 0)
        {
            return '"' + normalized.Replace("\"", "\"\"", StringComparison.Ordinal) + '"';
        }

        return normalized;
    }

    private static byte[] Encode(StringBuilder builder)
    {
        var content = builder.ToString();
        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(content)).ToArray();
    }
}
