using System.Globalization;
using AuditaPC.Core.Model;
using Microsoft.Win32;

namespace AuditaPC.Core.Inventory.Collectors;

public sealed class SoftwareCollector : IInventoryCollector
{
    private const string UninstallPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";

    public string Id => "software";
    public string Title => "Software instalado";
    public TimeSpan Timeout => TimeSpan.FromSeconds(20);

    public Task<CollectorOutcome> CollectAsync(CollectorContext context)
    {
        var checks = new List<CheckRecord>();
        var data = new SoftwareData
        {
            Limitations = new List<string>
            {
                "Solo se leen las claves de desinstalación del Registro accesibles.",
                "No se enumeran aplicaciones de Microsoft Store.",
                "No se detectan programas portables que no registran su instalación.",
                "Solo se consulta el perfil del usuario que ejecuta AuditaPC; no otros perfiles.",
                "Pueden existir entradas obsoletas, incompletas o duplicadas."
            }
        };

        var programs = new List<InstalledProgramInfo>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var anyHive = false;

        ReadHive(RegistryHive.LocalMachine, RegistryView.Registry64, "Equipo", "64 bits", "HKLM 64",
            programs, seen, checks, ref anyHive);
        ReadHive(RegistryHive.LocalMachine, RegistryView.Registry32, "Equipo", "32 bits", "HKLM 32",
            programs, seen, checks, ref anyHive);
        ReadHive(RegistryHive.CurrentUser, RegistryView.Registry64, "Usuario", "64 bits", "HKCU 64",
            programs, seen, checks, ref anyHive);
        ReadHive(RegistryHive.CurrentUser, RegistryView.Registry32, "Usuario", "32 bits", "HKCU 32",
            programs, seen, checks, ref anyHive);

        data.Programs = programs
            .OrderBy(program => program.Name, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(program => program.Version, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        data.TotalCount = data.Programs.Count;
        data.SystemScopeCount = data.Programs.Count(program => program.Scope == "Equipo");
        data.UserScopeCount = data.Programs.Count(program => program.Scope == "Usuario");

        if (anyHive)
        {
            checks.Add(CheckRecord.Verified("software.total", "Programas instalados",
                $"{data.TotalCount} programa(s) detectado(s) en el Registro",
                "Registro de Windows"));
        }
        else
        {
            checks.Add(CheckRecord.NotVerifiable("software.total", "Programas instalados",
                "No se pudo leer ninguna clave de desinstalación.",
                "Registro de Windows"));
        }

        checks.Add(CheckRecord.KnownLimitation(
            "software.coverage",
            "Cobertura de la detección",
            "La lista incluye programas registrados en el equipo y en el perfil actual. No incluye aplicaciones de Microsoft Store, programas portables ni instalaciones de otros perfiles.",
            "Limitación documentada"));

        var result = new CategoryResult<SoftwareData>
        {
            Id = Id,
            Title = Title,
            Data = data,
            Checks = checks,
            Status = CategoryStatus.Aggregate(checks, anyHive)
        };

        return Task.FromResult(new CollectorOutcome
        {
            Id = Id,
            Status = result.Status,
            Apply = snapshot => snapshot.Software = result
        });
    }

    public void ApplyUnavailable(InventorySnapshot target, CheckStatus status, string message, string? errorType)
    {
        target.Software = CategoryResult<SoftwareData>.Unavailable(Id, Title, status, message, errorType);
    }

    private static void ReadHive(
        RegistryHive hive,
        RegistryView view,
        string scope,
        string architecture,
        string label,
        List<InstalledProgramInfo> programs,
        HashSet<string> seen,
        List<CheckRecord> checks,
        ref bool anyHive)
    {
        var id = "software.hive." + label.Replace(' ', '-').ToLowerInvariant();
        try
        {
            using var key = RegistryHelper.OpenKey(hive, UninstallPath, view);
            if (key is null)
            {
                checks.Add(CheckRecord.NotDetected(id, $"Programas en {label}",
                    "La clave de desinstalación no existe o está vacía.", "Registro de Windows"));
                return;
            }

            anyHive = true;
            var count = 0;
            foreach (var subKeyName in RegistryHelper.GetSubKeyNames(key))
            {
                try
                {
                    using var subKey = key.OpenSubKey(subKeyName, writable: false);
                    if (subKey is null)
                    {
                        continue;
                    }

                    var name = Limit(RegistryHelper.GetString(subKey, "DisplayName"), 300);
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        continue;
                    }

                    if (RegistryHelper.GetInt(subKey, "SystemComponent") == 1)
                    {
                        continue;
                    }

                    if (!string.IsNullOrWhiteSpace(RegistryHelper.GetString(subKey, "ParentKeyName")))
                    {
                        continue;
                    }

                    var releaseType = RegistryHelper.GetString(subKey, "ReleaseType");
                    if (releaseType is not null
                        && (releaseType.Contains("Update", StringComparison.OrdinalIgnoreCase)
                            || releaseType.Contains("Hotfix", StringComparison.OrdinalIgnoreCase)
                            || releaseType.Contains("ServicePack", StringComparison.OrdinalIgnoreCase)))
                    {
                        continue;
                    }

                    var version = Limit(RegistryHelper.GetString(subKey, "DisplayVersion"), 100);
                    var publisher = Limit(RegistryHelper.GetString(subKey, "Publisher"), 200);
                    var dedupeKey = $"{name}\u001f{version}\u001f{publisher}";
                    if (!seen.Add(dedupeKey))
                    {
                        continue;
                    }

                    programs.Add(new InstalledProgramInfo
                    {
                        Name = name,
                        Version = version,
                        Publisher = publisher,
                        InstallDate = ParseInstallDate(RegistryHelper.GetString(subKey, "InstallDate")),
                        InstallLocation = Limit(RegistryHelper.GetString(subKey, "InstallLocation"), 300),
                        Scope = scope,
                        Architecture = architecture,
                        RegistryView = label,
                        ProductCode = Limit(subKeyName, 100)
                    });
                    count++;
                }
                catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
                {
                    checks.Add(CollectorErrors.AccessDenied(
                        id + ".item",
                        $"Entrada de {label}",
                        $"No se pudo leer una entrada del Registro: {ex.Message}",
                        "Registro de Windows"));
                }
            }

            checks.Add(CheckRecord.Verified(id, $"Programas en {label}",
                $"{count} programa(s) detectado(s)", "Registro de Windows"));
        }
        catch (RegistryReadException ex)
        {
            checks.Add(CollectorErrors.FromRegistry(id, $"Programas en {label}", ex));
        }
    }

    private static DateTimeOffset? ParseInstallDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.Length == 8
            && DateTime.TryParseExact(trimmed, "yyyyMMdd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var compact))
        {
            return new DateTimeOffset(compact);
        }

        return DateTimeOffset.TryParse(trimmed, CultureInfo.CurrentCulture,
            DateTimeStyles.AssumeLocal, out var parsed)
            ? parsed
            : null;
    }

    private static string? Limit(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength] + "…";
    }
}
