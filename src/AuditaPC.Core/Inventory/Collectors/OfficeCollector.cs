using System.Globalization;
using System.Text.RegularExpressions;
using AuditaPC.Core.Model;
using Microsoft.Win32;

namespace AuditaPC.Core.Inventory.Collectors;

public sealed class OfficeCollector : IInventoryCollector
{
    private static readonly Regex OfficeNamePattern = new(
        @"^Microsoft\s+(Office|Word|Excel|PowerPoint|Outlook|Access|Publisher|OneNote|Visio|Project|SharePoint Designer|Lync|Skype for Business)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex GuidPattern = new(
        @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}",
        RegexOptions.Compiled);

    private static readonly Dictionary<string, string> ProductNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["O365ProPlusRetail"] = "Microsoft 365 Apps para empresas",
        ["O365BusinessRetail"] = "Microsoft 365 Apps para empresas (Business)",
        ["O365SmallBusPremRetail"] = "Microsoft 365 Apps para pequeñas empresas",
        ["O365HomePremRetail"] = "Microsoft 365 (hogar)",
        ["O365EduCloudRetail"] = "Microsoft 365 Apps para educación",
        ["O365ProPlusEEANoTeamsRetail"] = "Microsoft 365 Apps para empresas (sin Teams)",
        ["ProPlus2024Retail"] = "Office LTSC 2024 Professional Plus",
        ["ProPlus2024Volume"] = "Office LTSC 2024 Professional Plus",
        ["ProPlus2021Retail"] = "Office LTSC 2021 Professional Plus",
        ["ProPlus2021Volume"] = "Office LTSC 2021 Professional Plus",
        ["ProPlus2019Retail"] = "Office 2019 Professional Plus",
        ["ProPlus2019Volume"] = "Office 2019 Professional Plus",
        ["Standard2021Volume"] = "Office LTSC 2021 Standard",
        ["Standard2019Volume"] = "Office 2019 Standard",
        ["Professional2021Retail"] = "Office LTSC 2021 Professional",
        ["Professional2019Retail"] = "Office 2019 Professional",
        ["HomeBusiness2021Retail"] = "Office Home & Business 2021",
        ["HomeStudent2021Retail"] = "Office Home & Student 2021",
        ["HomeBusiness2019Retail"] = "Office Home & Business 2019",
        ["HomeStudent2019Retail"] = "Office Home & Student 2019",
        ["VisioPro2021Retail"] = "Visio LTSC 2021 Professional",
        ["VisioPro2021Volume"] = "Visio LTSC 2021 Professional",
        ["VisioStd2021Retail"] = "Visio LTSC 2021 Standard",
        ["VisioStd2021Volume"] = "Visio LTSC 2021 Standard",
        ["VisioPro2019Retail"] = "Visio 2019 Professional",
        ["VisioPro2019Volume"] = "Visio 2019 Professional",
        ["ProjectPro2021Retail"] = "Project LTSC 2021 Professional",
        ["ProjectPro2021Volume"] = "Project LTSC 2021 Professional",
        ["ProjectStd2021Retail"] = "Project LTSC 2021 Standard",
        ["ProjectStd2021Volume"] = "Project LTSC 2021 Standard",
        ["ProjectPro2019Retail"] = "Project 2019 Professional",
        ["ProjectPro2019Volume"] = "Project 2019 Professional"
    };

    private static readonly Dictionary<string, string> ChannelNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["492350f6-3a01-4f97-b9c0-c7c6ddf67d60"] = "Canal actual",
        ["55336b82-a18d-4dd6-b5f6-9e5095c314a6"] = "Canal empresarial mensual",
        ["7ffbc6bf-bc32-4f92-8982-f9dd17fd3114"] = "Canal empresarial semestral",
        ["b8f9b850-328d-4355-9145-c59439a0c4cf"] = "Canal empresarial semestral (vista previa)",
        ["5440fd1f-7ecb-4221-8110-145efaa6372f"] = "Canal Beta",
        ["64256afe-f5d9-4f86-8936-8840a6a4f5be"] = "Canal actual (vista previa)",
        ["5030841d-c919-4594-8d2d-84ae4f96e58e"] = "Canal perpetuo (LTSC 2021)",
        ["f2e724c1-748f-4515-b67e-0d0b5b9c1b04"] = "Canal perpetuo (LTSC 2019)"
    };

    public string Id => "office";
    public string Title => "Microsoft Office";
    public TimeSpan Timeout => TimeSpan.FromSeconds(18);

    public async Task<CollectorOutcome> CollectAsync(CollectorContext context)
    {
        var checks = new List<CheckRecord>();
        var data = new OfficeData
        {
            DetectionNote = "Se detectan instalaciones Click-to-Run y MSI registradas. " +
                            "La vigencia contractual de suscripciones requiere autenticación o revisión del portal.",
            SubscriptionNote = "La activación local de Office no confirma la titularidad ni la vigencia " +
                               "contractual de una suscripción Microsoft 365. Se requiere autenticación o revisión del portal."
        };

        var installations = new List<OfficeInstallationInfo>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var detectionSucceeded = false;

        detectionSucceeded |= ReadClickToRun(RegistryView.Registry64, "64 bits", installations, seen, checks);
        detectionSucceeded |= ReadClickToRun(RegistryView.Registry32, "32 bits", installations, seen, checks);
        detectionSucceeded |= ReadMsiInstallations(RegistryView.Registry64, "64 bits", installations, seen, checks);
        detectionSucceeded |= ReadMsiInstallations(RegistryView.Registry32, "32 bits", installations, seen, checks);

        data.Installations = installations
            .OrderBy(installation => installation.ProductName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        data.Installed = data.Installations.Count > 0
            ? true
            : detectionSucceeded
                ? false
                : null;

        if (data.Installed == true)
        {
            checks.Add(CheckRecord.Detected("office.installed", "Instalación de Office",
                string.Join(" | ", data.Installations
                    .Select(installation => installation.ProductName)
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Distinct()),
                "Registro de Windows"));
        }
        else if (data.Installed == false)
        {
            checks.Add(CheckRecord.NotDetected("office.installed", "Instalación de Office",
                "No se detectaron instalaciones de Office registradas.",
                "Registro de Windows"));
        }
        else
        {
            checks.Add(CheckRecord.NotVerifiable("office.installed", "Instalación de Office",
                "No se pudo consultar el Registro de Office.",
                "Registro de Windows"));
        }

        var clickToRun = data.Installations.FirstOrDefault(
            installation => installation.InstallType == "Click-to-Run" && installation.Version is not null);
        var msi = data.Installations.FirstOrDefault(
            installation => installation.InstallType == "MSI" && installation.Version is not null);
        var versionSource = clickToRun ?? msi;
        if (versionSource is not null)
        {
            checks.Add(CheckRecord.Verified("office.version", "Versión y canal",
                $"{versionSource.ProductName} · versión {versionSource.Version}" +
                (string.IsNullOrWhiteSpace(versionSource.UpdateChannel)
                    ? string.Empty
                    : $" · {versionSource.UpdateChannel}"),
                versionSource.DetectedBy));
        }
        else if (data.Installed == true)
        {
            checks.Add(CheckRecord.NotVerifiable("office.version", "Versión y canal",
                "Office está instalado, pero no se pudo determinar versión y canal.",
                "Registro de Windows"));
        }

        var licensing = data.Installed == true
            ? await ReadOfficeLicensingAsync(data, checks, context.CancellationToken).ConfigureAwait(false)
            : (Verified: false, Problem: false, Message: "No se detectó Office instalado.",
                Source: "Registro de Windows");
        if (data.Installed == true)
        {
            if (licensing.Verified)
            {
                checks.Add(CheckRecord.Verified("office.license", "Licencia local de Office",
                    licensing.Message, licensing.Source));
            }
            else if (licensing.Problem)
            {
                checks.Add(CheckRecord.Problem("office.license", "Licencia local de Office",
                    licensing.Message, licensing.Source));
            }
            else
            {
                checks.Add(CheckRecord.NotVerifiable("office.license", "Licencia local de Office",
                    licensing.Message, licensing.Source));
            }

            checks.Add(context.EnableOnlineLicenseChecks
                ? CheckRecord.NotVerifiable(
                    "office.subscription",
                    "Vigencia de suscripción Microsoft 365",
                    "La conectividad con los servicios de Microsoft no confirma la cuenta, titularidad ni vigencia contractual. Se requiere autenticación o revisión del portal.",
                    "Microsoft 365")
                : CheckRecord.NotTestedOffline(
                    "office.subscription",
                    "Vigencia de suscripción Microsoft 365",
                    "No se consultó la vigencia contractual porque la comprobación en línea está desactivada. La activación local no demuestra que la suscripción siga vigente.",
                    "Microsoft 365"));

            checks.Add(context.EnableOnlineLicenseChecks
                ? await MicrosoftLicenseConnectivity.CheckOfficeAsync(context.CancellationToken).ConfigureAwait(false)
                : CheckRecord.NotTestedOffline(
                    "office.license.connectivity",
                    "Conectividad con licenciamiento de Office",
                    "No se intentó la conexión porque la comprobación en línea está desactivada en Configuración.",
                    "Servicios de licenciamiento Microsoft"));
        }
        else
        {
            checks.Add(CheckRecord.NotApplicable("office.license", "Licencia local de Office",
                "No se detectó Office instalado.", "WMI OfficeSoftwareProtectionPlatform"));
            checks.Add(CheckRecord.NotApplicable("office.subscription", "Vigencia de suscripción Microsoft 365",
                "No se detectó Office instalado.", "Microsoft 365"));
        }

        if (data.Installations.Count > 0)
        {
            var licenseType = InferLicenseType(data.Installations);
            checks.Add(licenseType is null
                ? CheckRecord.NotVerifiable("office.license-type", "Tipo de licenciamiento",
                    "No se pudo inferir el tipo de licenciamiento desde los identificadores de producto.",
                    "Registro de Windows")
                : CheckRecord.Detected("office.license-type", "Tipo de licenciamiento",
                    licenseType, "Registro de Windows"));
        }

        checks.Add(CheckRecord.KnownLimitation("office.coverage", "Cobertura de la detección",
            "La detección local identifica instalaciones y estado de activación informado por Windows. No confirma la titularidad ni vigencia contractual de suscripciones.",
            "Limitación documentada"));

        var result = new CategoryResult<OfficeData>
        {
            Id = Id,
            Title = Title,
            Data = data,
            Checks = checks,
            Status = CategoryStatus.Aggregate(
                checks,
                dataObtained: detectionSucceeded,
                primaryNegative: data.Installed == false)
        };

        return new CollectorOutcome
        {
            Id = Id,
            Status = result.Status,
            Apply = snapshot => snapshot.Office = result
        };
    }

    public void ApplyUnavailable(InventorySnapshot target, CheckStatus status, string message, string? errorType)
    {
        target.Office = CategoryResult<OfficeData>.Unavailable(Id, Title, status, message, errorType);
    }

    private static bool ReadClickToRun(
        RegistryView view,
        string architecture,
        List<OfficeInstallationInfo> installations,
        HashSet<string> seen,
        List<CheckRecord> checks)
    {
        var checkId = $"office.c2r.{(view == RegistryView.Registry64 ? "64" : "32")}";
        try
        {
            using var key = RegistryHelper.OpenKey(
                RegistryHive.LocalMachine,
                @"SOFTWARE\Microsoft\Office\ClickToRun\Configuration",
                view);
            if (key is null)
            {
                return false;
            }

            var releaseIds = RegistryHelper.GetString(key, "ProductReleaseIds");
            var version = Clean(RegistryHelper.GetString(key, "VersionToReport"));
            var platform = Clean(RegistryHelper.GetString(key, "Platform"));
            var updateChannel = Clean(RegistryHelper.GetString(key, "UpdateChannel"));
            var cdnBaseUrl = Clean(RegistryHelper.GetString(key, "CDNBaseUrl"));
            var installPath = Clean(RegistryHelper.GetString(key, "InstallPath"));
            var channel = ResolveChannel(updateChannel, cdnBaseUrl);
            var channelId = ResolveChannelId(updateChannel, cdnBaseUrl);

            if (string.IsNullOrWhiteSpace(releaseIds) && version is null)
            {
                return false;
            }

            var ids = (releaseIds ?? string.Empty)
                .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (ids.Count == 0)
            {
                ids.Add("ClickToRun");
            }

            foreach (var releaseId in ids)
            {
                var productName = MapProductName(releaseId);
                var dedupe = CreateDedupeKey(productName, version, architecture);
                if (!seen.Add(dedupe))
                {
                    continue;
                }

                installations.Add(new OfficeInstallationInfo
                {
                    ProductName = productName,
                    ReleaseId = releaseId,
                    Version = version,
                    Build = BuildFromVersion(version),
                    Architecture = platform ?? architecture,
                    UpdateChannel = channel,
                    ChannelId = channelId,
                    InstallType = "Click-to-Run",
                    InstallPath = installPath,
                    DetectedBy = "Registro ClickToRun Configuration"
                });
            }

            checks.Add(CheckRecord.Verified(checkId, $"Instalación Click-to-Run ({architecture})",
                $"{ids.Count} producto(s) configurado(s)" +
                (version is null ? string.Empty : $" · versión {version}"),
                "Registro ClickToRun Configuration"));
            return true;
        }
        catch (RegistryReadException ex)
        {
            checks.Add(CollectorErrors.FromRegistry(checkId, "Instalación Click-to-Run", ex));
            return false;
        }
    }

    private static bool ReadMsiInstallations(
        RegistryView view,
        string architecture,
        List<OfficeInstallationInfo> installations,
        HashSet<string> seen,
        List<CheckRecord> checks)
    {
        var checkId = $"office.msi.{(view == RegistryView.Registry64 ? "64" : "32")}";
        try
        {
            using var key = RegistryHelper.OpenKey(
                RegistryHive.LocalMachine,
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                view);
            if (key is null)
            {
                return false;
            }

            var found = 0;
            foreach (var subKeyName in RegistryHelper.GetSubKeyNames(key))
            {
                using var subKey = key.OpenSubKey(subKeyName, writable: false);
                if (subKey is null)
                {
                    continue;
                }

                var name = Clean(RegistryHelper.GetString(subKey, "DisplayName"));
                if (name is null || !OfficeNamePattern.IsMatch(name))
                {
                    continue;
                }

                if (name.Contains("Click-to-Run", StringComparison.OrdinalIgnoreCase)
                    || name.Contains("Microsoft 365", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var installLocation = Clean(RegistryHelper.GetString(subKey, "InstallLocation"));
                if (installLocation is not null
                    && installLocation.Contains(@"\root\", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var version = Clean(RegistryHelper.GetString(subKey, "DisplayVersion"));
                var major = ParseMajor(version);
                if (major is null or < 14)
                {
                    continue;
                }

                var windowsInstaller = RegistryHelper.GetInt(subKey, "WindowsInstaller");
                if (windowsInstaller != 1
                    && (installLocation is null
                        || !installLocation.Contains("Microsoft Office", StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                var dedupe = CreateDedupeKey(name, version, architecture);
                if (!seen.Add(dedupe))
                {
                    continue;
                }

                installations.Add(new OfficeInstallationInfo
                {
                    ProductName = name,
                    Version = version,
                    Build = version,
                    Architecture = architecture,
                    InstallType = "MSI",
                    InstallPath = installLocation,
                    DetectedBy = "Registro de desinstalación"
                });
                found++;
            }

            checks.Add(CheckRecord.Verified(checkId, $"Instalaciones MSI de Office ({architecture})",
                $"{found} instalación(es) MSI detectada(s)",
                "Registro de desinstalación"));
            return true;
        }
        catch (RegistryReadException ex)
        {
            checks.Add(CollectorErrors.FromRegistry(checkId, "Instalaciones MSI de Office", ex));
            return false;
        }
    }

    private static string CreateDedupeKey(string productName, string? version, string architecture)
    {
        // El mismo producto puede aparecer en Click-to-Run y en el registro
        // de desinstalación MSI con nombres y orden de palabras diferentes.
        var normalized = productName.ToLowerInvariant();
        var family = normalized.Contains("visio", StringComparison.Ordinal) ? "visio"
            : normalized.Contains("project", StringComparison.Ordinal) ? "project"
            : "office";
        var year = Regex.Match(normalized, @"20\d{2}").Value;
        var edition = normalized.Contains("professional plus", StringComparison.Ordinal)
                      || normalized.Contains("professionalplus", StringComparison.Ordinal)
                      || normalized.Contains("profesional plus", StringComparison.Ordinal)
                      || normalized.Contains("profesionalplus", StringComparison.Ordinal)
                      || normalized.Contains("proplus", StringComparison.Ordinal)
            ? "professionalplus"
            : normalized.Contains("professional", StringComparison.Ordinal)
              || normalized.Contains("profesional", StringComparison.Ordinal) ? "professional"
            : normalized.Contains("standard", StringComparison.Ordinal) ? "standard"
            : normalized.Contains("home & business", StringComparison.Ordinal)
              || normalized.Contains("homebusiness", StringComparison.Ordinal) ? "homebusiness"
            : normalized.Contains("home & student", StringComparison.Ordinal)
              || normalized.Contains("homestudent", StringComparison.Ordinal) ? "homestudent"
            : Regex.Replace(normalized, @"[^a-z0-9]", string.Empty);

        var arch = architecture.Contains("32", StringComparison.OrdinalIgnoreCase)
                   || architecture.Contains("x86", StringComparison.OrdinalIgnoreCase)
            ? "x86"
            : architecture.Contains("64", StringComparison.OrdinalIgnoreCase)
              || architecture.Contains("x64", StringComparison.OrdinalIgnoreCase) ? "x64" : architecture;

        return $"{family}\u001f{year}\u001f{edition}\u001f{version}\u001f{arch}";
    }

    private static async Task<(bool Verified, bool Problem, string Message, string Source)>
        ReadOfficeLicensingAsync(
            OfficeData data,
            List<CheckRecord> checks,
            CancellationToken cancellationToken)
    {
        const string wmiSource = "WMI OfficeSoftwareProtectionPlatform";
        try
        {
            var rows = WmiQuery.Query(
                "SoftwareLicensingProduct",
                namespacePath: @"root\OfficeSoftwareProtectionPlatform",
                timeout: TimeSpan.FromSeconds(9),
                selectColumns: "Name, Description, LicenseStatus, GracePeriodRemaining");

            if (rows.Count > 0)
            {
                foreach (var row in rows)
                {
                    var status = row.GetInt("LicenseStatus");
                    data.Licenses.Add(new OfficeLicenseInfo
                    {
                        ProductName = Clean(row.GetString("Name")),
                        Description = Clean(row.GetString("Description")),
                        LicenseStatus = status,
                        LicenseStatusText = LicenseStatusMapper.Map(status),
                        IsLicensed = LicenseStatusMapper.IsLicensed(status),
                        GracePeriodRemainingMinutes = row.GetInt("GracePeriodRemaining"),
                        Source = wmiSource
                    });
                }

                var licensed = data.Licenses.Count(license => license.LicenseStatus == 1);
                var allStatusesKnown = data.Licenses.All(license => license.LicenseStatus is not null);
                var problem = allStatusesKnown && licensed == 0;
                if (licensed > 0)
                {
                    return (true, false,
                        $"{licensed} producto(s) con licencia local activada: " +
                        string.Join(" | ", data.Licenses
                            .Where(license => license.LicenseStatus == 1)
                            .Select(license => license.ProductName)
                            .Where(name => !string.IsNullOrWhiteSpace(name))
                            .Distinct()),
                        wmiSource);
                }

                if (problem)
                {
                    var states = string.Join(" | ", data.Licenses
                        .Select(license => $"{license.ProductName}: {license.LicenseStatusText}")
                        .Where(text => !string.IsNullOrWhiteSpace(text))
                        .Distinct());
                    return (false, true,
                        "Office está instalado, pero no se encontró una licencia local activada. " + states,
                        wmiSource);
                }

                return (false, false, "WMI devolvió productos, pero no se pudo interpretar el estado de licencia.",
                    wmiSource);
            }

            var fallback = await ReadOsppLicensingAsync(data, cancellationToken).ConfigureAwait(false);
            return fallback.Found
                ? (fallback.Verified, fallback.Problem, fallback.Message, fallback.Source)
                : (false, false,
                    "WMI no devolvió productos y no se pudo obtener un estado mediante OSPP.VBS.",
                    "WMI / OSPP.VBS");
        }
        catch (WmiQueryException ex)
        {
            var fallback = await ReadOsppLicensingAsync(data, cancellationToken).ConfigureAwait(false);
            if (fallback.Found)
            {
                return (fallback.Verified, fallback.Problem, fallback.Message, fallback.Source);
            }

            if (ex.Kind == WmiFailureKind.NotFound)
            {
                return (false, false,
                    "Office Software Protection Platform no está disponible y no se encontró una consulta local alternativa.",
                    "WMI / OSPP.VBS");
            }

            checks.Add(CollectorErrors.FromWmi(
                "office.license.query", "Consulta de licencia de Office", ex));
            return (false, false, ex.Message, wmiSource);
        }
    }

    private static async Task<(bool Found, bool Verified, bool Problem, string Message, string Source)>
        ReadOsppLicensingAsync(OfficeData data, CancellationToken cancellationToken)
    {
        const string source = "OSPP.VBS /dstatus";
        var scriptPath = FindOsppScript(data.Installations);
        if (scriptPath is null)
        {
            return (false, false, false, "No se encontró OSPP.VBS.", source);
        }

        var cscriptPath = Path.Combine(Environment.SystemDirectory, "cscript.exe");
        if (!File.Exists(cscriptPath))
        {
            return (false, false, false, "No se encontró cscript.exe para consultar Office.", source);
        }

        var arguments = $"//nologo \"{scriptPath.Replace("\"", "\\\"")}\" /dstatus";
        var result = await ProcessRunner.RunAsync(
            cscriptPath, arguments, TimeSpan.FromSeconds(18), cancellationToken).ConfigureAwait(false);

        if (!result.Started || result.TimedOut || result.ExitCode != 0)
        {
            return (false, false, false,
                result.Error ?? "OSPP.VBS no pudo completar la consulta local.", source);
        }

        var output = result.StandardOutput + Environment.NewLine + result.StandardError;
        var parsedStatuses = new List<string?>();
        var blocks = Regex.Split(output, @"(?=^\s*LICENSE NAME\s*:)", RegexOptions.IgnoreCase | RegexOptions.Multiline);
        foreach (var block in blocks)
        {
            var name = ReadOsppField(block, "LICENSE NAME");
            if (string.IsNullOrWhiteSpace(name)
                || !name.Contains("Office", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var description = ReadOsppField(block, "LICENSE DESCRIPTION");
            var statusLine = ReadOsppField(block, "LICENSE STATUS");
            var statusMatch = Regex.Match(statusLine ?? string.Empty, @"---\s*(?<status>[A-Z_]+)\s*---",
                RegexOptions.IgnoreCase);
            var status = statusMatch.Success ? statusMatch.Groups["status"].Value.ToUpperInvariant() : null;
            parsedStatuses.Add(status);
            var statusText = MapOsppStatus(status);

            data.Licenses.Add(new OfficeLicenseInfo
            {
                ProductName = Clean(MatchOsppProductName(name, data.Installations) ?? name),
                Description = Clean(description),
                LicenseStatusText = statusText,
                IsLicensed = status == "LICENSED",
                Source = source
            });
        }

        if (data.Licenses.Count == 0)
        {
            return (false, false, false,
                "OSPP.VBS no devolvió estados de licencia de Office que se pudieran interpretar.", source);
        }

        var licensedCount = data.Licenses.Count(license => license.IsLicensed == true);
        var allStatusesKnown = parsedStatuses.Count > 0 && parsedStatuses.All(status =>
            status is "LICENSED" or "OOB_GRACE" or "OOT_GRACE" or "EXTENDED_GRACE" or "NOTIFICATIONS" or "UNLICENSED");
        if (licensedCount == data.Licenses.Count && allStatusesKnown)
        {
            var products = string.Join(" | ", data.Licenses
                .Where(license => license.IsLicensed == true)
                .Select(license => license.ProductName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct());
            return (true, true, false,
                $"{licensedCount} producto(s) con activación local confirmada por OSPP: {products}. " +
                "Esto no acredita titularidad ni vigencia contractual.",
                source);
        }

        var details = string.Join(" | ", data.Licenses
            .Select(license => $"{license.ProductName}: {license.LicenseStatusText}")
            .Distinct());
        var someProductsNotLicensed = allStatusesKnown && licensedCount < data.Licenses.Count;
        return (true, false, someProductsNotLicensed,
            someProductsNotLicensed
                ? $"Solo {licensedCount} de {data.Licenses.Count} productos tienen licencia local activa. " + details
                : "OSPP.VBS devolvió productos, pero no se pudo interpretar todos los estados. " + details,
            source);
    }

    private static string? MatchOsppProductName(
        string osppName,
        IEnumerable<OfficeInstallationInfo> installations)
    {
        var normalized = osppName.ToLowerInvariant();
        var product = normalized.Contains("project", StringComparison.Ordinal) ? "project"
            : normalized.Contains("visio", StringComparison.Ordinal) ? "visio"
            : normalized.Contains("proplus", StringComparison.Ordinal) ? "professional plus"
            : normalized.Contains("standard", StringComparison.Ordinal) ? "standard"
            : normalized.Contains("professional", StringComparison.Ordinal) ? "professional"
            : null;

        return product is null
            ? null
            : installations.FirstOrDefault(installation =>
                installation.ProductName?.Contains(product, StringComparison.OrdinalIgnoreCase) == true)
                ?.ProductName;
    }

    private static string? FindOsppScript(IEnumerable<OfficeInstallationInfo> installations)
    {
        var candidates = new List<string>();
        foreach (var installPath in installations
                     .Select(item => item.InstallPath)
                     .Where(path => !string.IsNullOrWhiteSpace(path)))
        {
            candidates.Add(Path.Combine(installPath!, "OSPP.VBS"));
            candidates.Add(Path.Combine(installPath!, "Office16", "OSPP.VBS"));
            candidates.Add(Path.Combine(installPath!, "root", "Office16", "OSPP.VBS"));
        }

        foreach (var programFiles in new[]
                 {
                     Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                     Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
                 }.Where(path => !string.IsNullOrWhiteSpace(path)))
        {
            candidates.Add(Path.Combine(programFiles, "Microsoft Office", "Office16", "OSPP.VBS"));
            candidates.Add(Path.Combine(programFiles, "Microsoft Office", "root", "Office16", "OSPP.VBS"));
        }

        return candidates.Distinct(StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(File.Exists);
    }

    private static string? ReadOsppField(string block, string field)
    {
        var match = Regex.Match(block,
            @"^\s*" + Regex.Escape(field) + @"\s*:\s*(?<value>.+?)\s*$",
            RegexOptions.IgnoreCase | RegexOptions.Multiline);
        return match.Success ? match.Groups["value"].Value.Trim() : null;
    }

    private static string MapOsppStatus(string? status) => status switch
    {
        "LICENSED" => "Licenciado",
        "OOB_GRACE" => "Período de gracia inicial",
        "OOT_GRACE" => "Período de gracia extendido",
        "EXTENDED_GRACE" => "Período de gracia extendido",
        "NOTIFICATIONS" => "Notificaciones",
        "UNLICENSED" => "Sin licencia",
        _ => "Estado no reconocido"
    };

    private static string? InferLicenseType(IEnumerable<OfficeInstallationInfo> installations)
    {
        var ids = installations
            .Select(installation => installation.ReleaseId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id!)
            .ToList();

        if (ids.Count == 0)
        {
            return null;
        }

        if (ids.Any(id => id.StartsWith("O365", StringComparison.OrdinalIgnoreCase)))
        {
            return "Suscripción Microsoft 365 (según identificador de producto)";
        }

        if (ids.Any(id => id.Contains("Volume", StringComparison.OrdinalIgnoreCase)))
        {
            return "Licencia por volumen (según identificador de producto)";
        }

        if (ids.Any(id => id.Contains("Retail", StringComparison.OrdinalIgnoreCase)))
        {
            return "Licencia retail u OEM (según identificador de producto)";
        }

        return null;
    }

    private static string MapProductName(string releaseId)
    {
        if (ProductNames.TryGetValue(releaseId, out var name))
        {
            return name;
        }

        if (releaseId.Equals("ClickToRun", StringComparison.OrdinalIgnoreCase))
        {
            return "Microsoft Office (Click-to-Run)";
        }

        if (releaseId.StartsWith("O365", StringComparison.OrdinalIgnoreCase))
        {
            return "Microsoft 365 Apps";
        }

        if (releaseId.Contains("Visio", StringComparison.OrdinalIgnoreCase))
        {
            return "Microsoft Visio";
        }

        if (releaseId.Contains("Project", StringComparison.OrdinalIgnoreCase))
        {
            return "Microsoft Project";
        }

        if (releaseId.Contains("ProPlus", StringComparison.OrdinalIgnoreCase))
        {
            return "Microsoft Office Professional Plus";
        }

        if (releaseId.Contains("Standard", StringComparison.OrdinalIgnoreCase))
        {
            return "Microsoft Office Standard";
        }

        return releaseId;
    }

    private static string? ResolveChannel(string? updateChannel, string? cdnBaseUrl)
    {
        var id = ResolveChannelId(updateChannel, cdnBaseUrl);
        if (id is not null && ChannelNames.TryGetValue(id, out var name))
        {
            return name;
        }

        if (!string.IsNullOrWhiteSpace(updateChannel))
        {
            if (updateChannel.Contains("current", StringComparison.OrdinalIgnoreCase))
            {
                return "Canal actual";
            }

            // Un GUID o una URL de CDN sin correspondencia conocida no aporta
            // información legible al técnico; se omite en lugar de mostrarla en bruto.
            if (GuidPattern.IsMatch(updateChannel) || updateChannel.Contains("://", StringComparison.Ordinal))
            {
                return null;
            }

            return updateChannel;
        }

        return null;
    }

    private static string? ResolveChannelId(string? updateChannel, string? cdnBaseUrl)
    {
        foreach (var candidate in new[] { updateChannel, cdnBaseUrl })
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            var match = GuidPattern.Match(candidate);
            if (match.Success)
            {
                return match.Value.ToLowerInvariant();
            }
        }

        return null;
    }

    private static string? BuildFromVersion(string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return null;
        }

        var parts = version.Split('.');
        return parts.Length >= 4 ? $"{parts[2]}.{parts[3]}" : version;
    }

    private static int? ParseMajor(string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return null;
        }

        var first = version.Split('.')[0];
        return int.TryParse(first, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
