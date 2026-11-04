using System.Globalization;
using AuditaPC.Core.Model;
using Microsoft.Win32;

namespace AuditaPC.Core.Inventory.Collectors;

public sealed class OperatingSystemCollector : IInventoryCollector
{
    private const string WindowsApplicationId = "55c92734-d682-4d71-983e-d6ec3f16059f";

    public string Id => "os";
    public string Title => "Sistema operativo";
    public TimeSpan Timeout => TimeSpan.FromSeconds(16);

    public async Task<CollectorOutcome> CollectAsync(CollectorContext context)
    {
        var checks = new List<CheckRecord>();
        var data = new OsData
        {
            SystemLocale = CultureInfo.InstalledUICulture.DisplayName,
            SystemDrive = Environment.GetEnvironmentVariable("SystemDrive")
        };

        var obtained = false;

        var (osRows, osError) = TryQuery("Win32_OperatingSystem", TimeSpan.FromSeconds(7));
        if (osRows.Count > 0)
        {
            var row = osRows[0];
            data.ProductName = Clean(row.GetString("Caption"));
            data.Version = Clean(row.GetString("Version"));
            data.BuildNumber = Clean(row.GetString("BuildNumber"));
            data.Architecture = Clean(row.GetString("OSArchitecture"));
            data.LastBootTime = row.GetDateTime("LastBootUpTime");
            data.SystemDrive ??= Clean(row.GetString("SystemDrive"));
            var wmiInstallDate = row.GetDateTime("InstallDate");
            if (wmiInstallDate is not null)
            {
                data.InstallDate = wmiInstallDate;
                data.InstallDateSource = "WMI Win32_OperatingSystem (InstallDate)";
            }

            obtained = true;
            checks.Add(CheckRecord.Verified("os.product", "Producto de Windows",
                data.ProductName, "WMI Win32_OperatingSystem"));
        }
        else
        {
            checks.Add(osError is null
                ? CheckRecord.NotVerifiable("os.product", "Producto de Windows",
                    "Windows no devolvió datos de Win32_OperatingSystem.", "WMI Win32_OperatingSystem")
                : CollectorErrors.FromWmi("os.product", "Producto de Windows", osError));
        }

        var registryOk = ReadCurrentVersionRegistry(data, checks);
        if (registryOk)
        {
            obtained = true;
        }

        if (!string.IsNullOrWhiteSpace(data.BuildNumber)
            || !string.IsNullOrWhiteSpace(data.DisplayVersion))
        {
            var build = ParseBuild(data.BuildNumber) ?? ParseBuild(ReadRegistryString(
                @"SOFTWARE\Microsoft\Windows NT\CurrentVersion", "CurrentBuild"));
            data.FriendlyName = BuildFriendlyName(build, data.DisplayVersion);
            checks.Add(CheckRecord.Verified("os.version", "Versión y compilación",
                $"{data.FriendlyName} · {data.Version} · compilación {data.BuildNumber}" +
                (data.UpdateBuildRevision is { } ubr ? $".{ubr}" : string.Empty),
                "Registro de Windows / WMI"));
        }
        else
        {
            checks.Add(CheckRecord.NotVerifiable("os.version", "Versión y compilación",
                "No se pudo determinar la versión de Windows.", "Registro de Windows / WMI"));
        }

        checks.Add(string.IsNullOrWhiteSpace(data.Architecture)
            ? CheckRecord.NotVerifiable("os.arch", "Arquitectura del sistema",
                "Windows no informó la arquitectura.", "WMI Win32_OperatingSystem")
            : CheckRecord.Verified("os.arch", "Arquitectura del sistema",
                data.Architecture, "WMI Win32_OperatingSystem"));

        ReadInstallDate(data, checks);
        ReadWindowsLicense(data, checks, context.EnableOnlineLicenseChecks);
        ReadRecentUpdates(data, checks);

        checks.Add(CheckRecord.NotTestedOffline(
            "os.pending-updates",
            "Actualizaciones pendientes",
            "No se consultó Windows Update durante esta captura. La comprobación requiere Internet y se mantiene fuera del levantamiento local.",
            "Windows Update"));

        checks.Add(context.EnableOnlineLicenseChecks
            ? await MicrosoftLicenseConnectivity.CheckWindowsAsync(context.CancellationToken).ConfigureAwait(false)
            : CheckRecord.NotTestedOffline(
                "os.license.connectivity",
                "Conectividad con activación de Windows",
                "No se intentó la conexión porque la comprobación en línea está desactivada en Configuración.",
                "Servicio de activación de Microsoft"));

        var result = new CategoryResult<OsData>
        {
            Id = Id,
            Title = Title,
            Data = data,
            Checks = checks,
            Status = CategoryStatus.Aggregate(checks, obtained)
        };

        return new CollectorOutcome
        {
            Id = Id,
            Status = result.Status,
            Apply = snapshot => snapshot.OperatingSystem = result
        };
    }

    public void ApplyUnavailable(InventorySnapshot target, CheckStatus status, string message, string? errorType)
    {
        target.OperatingSystem = CategoryResult<OsData>.Unavailable(Id, Title, status, message, errorType);
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

    private static bool ReadCurrentVersionRegistry(OsData data, List<CheckRecord> checks)
    {
        try
        {
            using var key = RegistryHelper.OpenKey(
                RegistryHive.LocalMachine, @"SOFTWARE\Microsoft\Windows NT\CurrentVersion",
                RegistryView.Registry64);

            if (key is null)
            {
                checks.Add(CheckRecord.NotVerifiable("os.registry", "Datos de Windows en el Registro",
                    "No se encontró la clave CurrentVersion.", "Registro de Windows"));
                return false;
            }

            data.ProductName ??= Clean(RegistryHelper.GetString(key, "ProductName"));
            data.Edition = MapEdition(RegistryHelper.GetString(key, "EditionID"));
            data.DisplayVersion = Clean(RegistryHelper.GetString(key, "DisplayVersion"));
            data.InstallationType = Clean(RegistryHelper.GetString(key, "InstallationType"));
            data.UpdateBuildRevision = RegistryHelper.GetInt(key, "UBR");
            data.BuildNumber ??= Clean(RegistryHelper.GetString(key, "CurrentBuild"));
            var major = RegistryHelper.GetInt(key, "CurrentMajorVersionNumber");
            var minor = RegistryHelper.GetInt(key, "CurrentMinorVersionNumber");
            if (data.Version is null && major is not null && minor is not null)
            {
                data.Version = $"{major}.{minor}";
            }

            checks.Add(CheckRecord.Verified("os.registry", "Datos de Windows en el Registro",
                $"Edición: {Formatting.OrDash(data.Edition)}; versión: {Formatting.OrDash(data.DisplayVersion)}",
                "Registro de Windows"));
            return true;
        }
        catch (RegistryReadException ex)
        {
            checks.Add(CollectorErrors.FromRegistry("os.registry", "Datos de Windows en el Registro", ex));
            return false;
        }
    }

    private static void ReadInstallDate(OsData data, List<CheckRecord> checks)
    {
        try
        {
            using var key = RegistryHelper.OpenKey(
                RegistryHive.LocalMachine, @"SOFTWARE\Microsoft\Windows NT\CurrentVersion",
                RegistryView.Registry64);
            var fromRegistry = RegistryHelper.GetUnixSeconds(key, "InstallDate");
            if (fromRegistry is not null)
            {
                data.InstallDate = fromRegistry;
                data.InstallDateSource = "Registro de Windows (InstallDate)";
                checks.Add(CheckRecord.Verified("os.install", "Fecha de instalación de Windows",
                    Formatting.Date(data.InstallDate), data.InstallDateSource));
                return;
            }
        }
        catch (RegistryReadException ex)
        {
            checks.Add(CollectorErrors.FromRegistry("os.install", "Fecha de instalación de Windows", ex));
        }

        if (data.InstallDate is null)
        {
            checks.Add(CheckRecord.NotVerifiable("os.install", "Fecha de instalación de Windows",
                "No se pudo determinar una fecha de instalación fiable.",
                "Registro de Windows / WMI"));
        }
        else
        {
            checks.Add(CheckRecord.Verified("os.install", "Fecha de instalación de Windows",
                Formatting.Date(data.InstallDate), data.InstallDateSource));
        }
    }

    private static void ReadWindowsLicense(
        OsData data,
        List<CheckRecord> checks,
        bool onlineChecksEnabled)
    {
        var license = data.License;
        try
        {
            var rows = WmiQuery.Query(
                "SoftwareLicensingProduct",
                where: $"ApplicationID='{WindowsApplicationId}'",
                timeout: TimeSpan.FromSeconds(8),
                selectColumns: "Name, Description, LicenseStatus, GracePeriodRemaining");

            if (rows.Count == 0)
            {
                license.QueryNote = "Windows no devolvió productos de licenciamiento.";
                checks.Add(CheckRecord.NotVerifiable("os.license", "Activación de Windows",
                    license.QueryNote, "WMI SoftwareLicensingProduct"));
                AddSubscriptionCheck(checks, onlineChecksEnabled);
                return;
            }

            var windowsRows = rows
                .Where(row => ContainsWindows(row.GetString("Name")) || ContainsWindows(row.GetString("Description")))
                .ToList();
            var candidates = windowsRows.Count > 0 ? windowsRows : rows;
            var selected = candidates.FirstOrDefault(row => row.GetInt("LicenseStatus") == 1)
                           ?? candidates[0];

            license.ProductName = Clean(selected.GetString("Name"));
            license.Description = Clean(selected.GetString("Description"));
            license.LicenseStatus = selected.GetInt("LicenseStatus");
            license.LicenseStatusText = LicenseStatusMapper.Map(license.LicenseStatus);
            license.IsLicensed = LicenseStatusMapper.IsLicensed(license.LicenseStatus);
            license.GracePeriodRemainingMinutes = selected.GetInt("GracePeriodRemaining");
            license.Channel = LicenseStatusMapper.ExtractChannel(license.Description);
            license.Source = "WMI SoftwareLicensingProduct";

            if (license.LicenseStatus is null)
            {
                checks.Add(CheckRecord.NotVerifiable("os.license", "Activación de Windows",
                    "Windows devolvió un producto de licencia sin estado interpretable.",
                    "WMI SoftwareLicensingProduct"));
            }
            else if (license.LicenseStatus == 1)
            {
                checks.Add(CheckRecord.Verified("os.license", "Activación de Windows",
                    $"Licenciado · {Formatting.OrDash(license.ProductName)}" +
                    (license.Channel is null ? string.Empty : $" · canal {license.Channel}"),
                    "WMI SoftwareLicensingProduct"));
            }
            else
            {
                var detail = $"{license.LicenseStatusText} (código {license.LicenseStatus})";
                if (license.GracePeriodRemainingMinutes is > 0)
                {
                    detail += $" · gracia restante: {license.GracePeriodRemainingMinutes} min";
                }

                checks.Add(CheckRecord.Problem("os.license", "Activación de Windows",
                    detail, "WMI SoftwareLicensingProduct"));
            }
        }
        catch (WmiQueryException ex)
        {
            checks.Add(CollectorErrors.FromWmi("os.license", "Activación de Windows", ex));
        }

        AddSubscriptionCheck(checks, onlineChecksEnabled);
    }

    private static void AddSubscriptionCheck(List<CheckRecord> checks, bool onlineChecksEnabled)
    {
        checks.Add(onlineChecksEnabled
            ? CheckRecord.NotVerifiable(
                "os.license.subscription",
                "Vigencia contractual de la licencia",
                "La conectividad con el servicio de activación no confirma la titularidad ni vigencia contractual. Se requiere autenticación o revisión del portal de licencias.",
                "Microsoft")
            : CheckRecord.NotTestedOffline(
                "os.license.subscription",
                "Vigencia contractual de la licencia",
                "No se consultó la vigencia contractual porque la comprobación en línea está desactivada. La activación local no demuestra la vigencia de una suscripción.",
                "Microsoft"));
    }

    private static void ReadRecentUpdates(OsData data, List<CheckRecord> checks)
    {
        try
        {
            var rows = WmiQuery.Query(
                "Win32_QuickFixEngineering",
                timeout: TimeSpan.FromSeconds(7),
                selectColumns: "HotFixID, Description, InstalledOn, InstalledBy");

            data.RecentUpdates = rows
                .Select(row => new WindowsUpdateInfo
                {
                    HotFixId = Clean(row.GetString("HotFixID")),
                    Description = Clean(row.GetString("Description")),
                    InstalledOn = GetFlexibleDate(row, "InstalledOn"),
                    InstalledBy = Clean(row.GetString("InstalledBy"))
                })
                .OrderByDescending(update => update.InstalledOn ?? DateTimeOffset.MinValue)
                .Take(10)
                .ToList();

            checks.Add(CheckRecord.Verified("os.updates", "Actualizaciones instaladas",
                data.RecentUpdates.Count == 0
                    ? "Windows no informó actualizaciones instaladas."
                    : $"{data.RecentUpdates.Count} actualizaciones recientes localizadas.",
                "WMI Win32_QuickFixEngineering"));
        }
        catch (WmiQueryException ex)
        {
            checks.Add(CollectorErrors.FromWmi("os.updates", "Actualizaciones instaladas", ex));
        }
    }

    private static DateTimeOffset? GetFlexibleDate(WmiRow row, string name)
    {
        var direct = row.GetDateTime(name);
        if (direct is not null)
        {
            return direct;
        }

        var text = row.GetString(name);
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        return DateTimeOffset.TryParse(text, CultureInfo.CurrentCulture,
            DateTimeStyles.AssumeLocal, out var parsed)
            ? parsed
            : null;
    }

    private static string? ReadRegistryString(string subKey, string value)
    {
        try
        {
            using var key = RegistryHelper.OpenKey(RegistryHive.LocalMachine, subKey, RegistryView.Registry64);
            return Clean(RegistryHelper.GetString(key, value));
        }
        catch
        {
            return null;
        }
    }

    private static int? ParseBuild(string? build) =>
        int.TryParse(build, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;

    private static string BuildFriendlyName(int? build, string? displayVersion)
    {
        var name = build switch
        {
            >= 22000 => "Windows 11",
            >= 10240 => "Windows 10",
            null => "Windows",
            _ => "Windows (versión anterior)"
        };

        return string.IsNullOrWhiteSpace(displayVersion) ? name : $"{name} {displayVersion}";
    }

    private static string? MapEdition(string? editionId) => editionId?.Trim() switch
    {
        null or "" => null,
        "Core" => "Home",
        "CoreSingleLanguage" => "Home Single Language",
        "Professional" => "Pro",
        "ProfessionalN" => "Pro N",
        "Enterprise" => "Enterprise",
        "EnterpriseN" => "Enterprise N",
        "Education" => "Education",
        "EducationN" => "Education N",
        "IoTEnterprise" => "IoT Enterprise",
        "ServerStandard" => "Server Standard",
        "ServerDatacenter" => "Server Datacenter",
        _ => editionId.Trim()
    };

    private static bool ContainsWindows(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Contains("Windows", StringComparison.OrdinalIgnoreCase);

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
