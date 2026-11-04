using System.Collections.ObjectModel;
using AuditaPC.Core;
using AuditaPC.Core.Model;

namespace AuditaPC.App.ViewModels;

public sealed class DetailRow
{
    public DetailRow(string label, string? value, bool mono = false)
    {
        Label = label;
        Value = string.IsNullOrWhiteSpace(value) ? "—" : value.Trim();
        IsMono = mono;
    }

    public string Label { get; }
    public string Value { get; }
    public bool IsMono { get; }
}

public sealed class CheckRow
{
    public CheckRow(string category, CheckRecord check)
    {
        Category = category;
        Label = check.Label;
        Status = check.Status;
        Detail = check.Detail ?? "—";
        Source = check.Source ?? "—";
    }

    public string Category { get; }
    public string Label { get; }
    public CheckStatus Status { get; }
    public string StatusText => Status.ToDisplayText();
    public string Detail { get; }
    public string Source { get; }
    public string StatusExplanation => Status switch
    {
        CheckStatus.Verified => "La consulta se realizó correctamente.",
        CheckStatus.Detected => "Elemento encontrado; no certifica su estado completo.",
        CheckStatus.NotDetected => "La consulta se realizó y no encontró el elemento.",
        CheckStatus.Problem => "Se confirmó una condición que debe revisar el técnico.",
        CheckStatus.Partial => "La información se obtuvo parcialmente. Revisa el detalle.",
        CheckStatus.NotVerifiable => "No hubo evidencia suficiente; revisa el detalle y la fuente.",
        CheckStatus.RequiresElevation => "Repite la captura con AuditaPC ejecutándose como administrador.",
        CheckStatus.PermissionDenied => "El sistema o una política restringió el acceso.",
        CheckStatus.NotTestedOffline => "No se consultó: requiere Internet o la opción online está desactivada.",
        CheckStatus.KnownLimitation => "Este dato queda fuera del alcance de la versión actual.",
        CheckStatus.NotApplicable => "Esta comprobación no corresponde a este equipo.",
        CheckStatus.QueryError => "La consulta produjo un error técnico; revisa el detalle.",
        CheckStatus.Timeout => "La consulta superó el tiempo máximo permitido.",
        _ => "Estado no reconocido."
    };
    public bool RequiresReview => Status.RequiresReview();
}

public sealed class ProgramRow
{
    public ProgramRow(string name, string? version, string? publisher, string? date, string? scope)
    {
        Name = string.IsNullOrWhiteSpace(name) ? "—" : name;
        Version = string.IsNullOrWhiteSpace(version) ? "—" : version;
        Publisher = string.IsNullOrWhiteSpace(publisher) ? "—" : publisher;
        Date = string.IsNullOrWhiteSpace(date) ? "—" : date;
        Scope = string.IsNullOrWhiteSpace(scope) ? "—" : scope;
    }

    public string Name { get; }
    public string Version { get; }
    public string Publisher { get; }
    public string Date { get; }
    public string Scope { get; }
}

public sealed class CategoryViewModel
{
    public string Id { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public CheckStatus Status { get; init; }
    public string StatusText => Status.ToDisplayText();
    public string Summary { get; init; } = string.Empty;
    public ObservableCollection<DetailRow> Rows { get; } = new();
    public ObservableCollection<CheckRow> Checks { get; } = new();
    public ObservableCollection<ProgramRow> Programs { get; } = new();
    public string ProgramsTitle { get; init; } = "Programas instalados";
    public bool HasPrograms => Programs.Count > 0;
    public string ProgramCountText => Programs.Count == 1
        ? "1 programa detectado"
        : $"{Programs.Count} programas detectados";
    public bool HasChecks => Checks.Count > 0;
    public bool HasRows => Rows.Count > 0;
    public int ProblemCount => Checks.Count(check => check.RequiresReview);
}

public static class InventoryViewModelBuilder
{
    public static List<CategoryViewModel> Build(InventorySnapshot? snapshot)
    {
        var categories = new List<CategoryViewModel>();
        if (snapshot is null)
        {
            return categories;
        }

        categories.Add(BuildIdentity(snapshot));
        categories.Add(BuildOperatingSystem(snapshot));
        categories.Add(BuildHardware(snapshot));
        categories.Add(BuildStorage(snapshot));
        categories.Add(BuildNetwork(snapshot));
        categories.Add(BuildSoftware(snapshot));
        categories.Add(BuildOffice(snapshot));
        categories.Add(BuildSecurity(snapshot));
        return categories;
    }

    public static List<CheckRow> BuildChecks(InventorySnapshot? snapshot)
    {
        if (snapshot is null)
        {
            return new List<CheckRow>();
        }

        return snapshot.Categories()
            .SelectMany(category => category.Checks.Select(check => new CheckRow(category.Title, check)))
            .ToList();
    }

    private static CategoryViewModel BuildIdentity(InventorySnapshot snapshot)
    {
        var category = snapshot.Identity;
        var data = category.Data;
        var model = new CategoryViewModel
        {
            Id = category.Id,
            Title = category.Title,
            Status = category.Status,
            Summary = Join(data?.Manufacturer, data?.Model)
        };

        Add(model, "Equipo", data?.ComputerName);
        Add(model, "Fabricante", data?.Manufacturer);
        Add(model, "Modelo", data?.Model);
        Add(model, "Tipo de equipo", data?.ChassisType ?? data?.SystemType);
        Add(model, "Arquitectura", data?.Architecture);
        Add(model, "Firmware", data?.FirmwareType);
        Add(model, "BIOS", Join(data?.BiosVendor, data?.BiosVersion));
        Add(model, "Fecha del BIOS", Formatting.Date(data?.BiosReleaseDate));
        Add(model, "Serie del BIOS", data?.BiosSerial, mono: true);
        Add(model, "UUID SMBIOS", data?.SystemUuid, mono: true);
        Add(model, "Placa base", Join(data?.BaseboardManufacturer, data?.BaseboardProduct));
        Add(model, "Serie de placa base", data?.BaseboardSerial, mono: true);
        Add(model, "GUID de instalación de Windows", data?.MachineGuid, mono: true);
        Add(model, "Dominio o grupo", data?.Domain ?? data?.Workgroup);
        Add(model, "Unido a Entra ID", Formatting.YesNo(data?.EntraJoined));
        Add(model, "Inquilino de Entra ID", data?.EntraTenantName);
        Add(model, "Usuario de la captura", Join(data?.CurrentUserDomain, data?.CurrentUser));
        Add(model, "Elevación", Formatting.YesNo(data?.IsElevated));
        Add(model, "Virtualización", data?.VirtualizationHint
            ?? Formatting.YesNo(data?.IsVirtualMachine, "Sí", "No"));
        AddChecks(model, category);
        return model;
    }

    private static CategoryViewModel BuildOperatingSystem(InventorySnapshot snapshot)
    {
        var category = snapshot.OperatingSystem;
        var data = category.Data;
        var model = new CategoryViewModel
        {
            Id = category.Id,
            Title = category.Title,
            Status = category.Status,
            Summary = data?.FriendlyName ?? data?.ProductName ?? string.Empty
        };

        Add(model, "Producto", data?.ProductName);
        Add(model, "Edición", data?.Edition);
        Add(model, "Versión", data?.DisplayVersion);
        Add(model, "Compilación", JoinBuild(data?.Version, data?.BuildNumber, data?.UpdateBuildRevision));
        Add(model, "Arquitectura", data?.Architecture);
        Add(model, "Instalación", Formatting.DateTimeLong(data?.InstallDate));
        Add(model, "Último arranque", Formatting.DateTimeLong(data?.LastBootTime));
        Add(model, "Tipo de instalación", data?.InstallationType);
        Add(model, "Disco del sistema", data?.SystemDrive);
        Add(model, "Idioma", data?.SystemLocale);
        Add(model, "Licencia", data?.License.ProductName);
        Add(model, "Estado de licencia", data?.License.LicenseStatusText);
        Add(model, "Canal de licencia", data?.License.Channel);
        Add(model, "Nota de licencia", data?.License.QueryNote);
        AddChecks(model, category);
        return model;
    }

    private static CategoryViewModel BuildHardware(InventorySnapshot snapshot)
    {
        var category = snapshot.Hardware;
        var data = category.Data;
        var cpu = data?.Processors.FirstOrDefault();
        var model = new CategoryViewModel
        {
            Id = category.Id,
            Title = category.Title,
            Status = category.Status,
            Summary = Join(cpu?.Name, data?.TotalMemoryBytes is { } memory ? Formatting.Bytes(memory) + " RAM" : null)
        };

        foreach (var processor in data?.Processors ?? new List<CpuInfo>())
        {
            Add(model, "Procesador",
                $"{processor.Name} · {processor.Cores} núcleos / {processor.LogicalProcessors} hilos · " +
                $"{processor.MaxClockMhz} MHz");
        }

        Add(model, "Memoria RAM", Formatting.Bytes(data?.TotalMemoryBytes));
        Add(model, "Ranuras de memoria", data?.MemorySlotCount?.ToString());
        Add(model, "Capacidad máxima", Formatting.Bytes(data?.MemoryMaxCapacityBytes));

        var moduleIndex = 1;
        foreach (var module in data?.MemoryModules ?? new List<MemoryModuleInfo>())
        {
            Add(model, $"Módulo {moduleIndex++}",
                $"{Formatting.Bytes(module.CapacityBytes)} · {module.SpeedMhz} MHz · " +
                $"{module.MemoryType} · {module.Manufacturer} {module.PartNumber}");
        }

        foreach (var gpu in data?.Graphics ?? new List<GraphicsAdapterInfo>())
        {
            Add(model, "Video", $"{gpu.Name} · {Formatting.Bytes(gpu.AdapterRamBytes)} · controlador {gpu.DriverVersion}");
        }

        foreach (var battery in data?.Batteries ?? new List<BatteryInfo>())
        {
            Add(model, "Batería", $"{battery.Name} · carga {battery.EstimatedChargePercent} % · {battery.Status}");
        }

        AddChecks(model, category);
        return model;
    }

    private static CategoryViewModel BuildStorage(InventorySnapshot snapshot)
    {
        var category = snapshot.Storage;
        var data = category.Data;
        var total = data?.PhysicalDisks.Sum(disk => disk.SizeBytes ?? 0) ?? 0;
        var model = new CategoryViewModel
        {
            Id = category.Id,
            Title = category.Title,
            Status = category.Status,
            Summary = $"{data?.PhysicalDisks.Count ?? 0} disco(s) · {Formatting.Bytes(total)}"
        };

        var index = 1;
        foreach (var disk in data?.PhysicalDisks ?? new List<PhysicalDiskInfo>())
        {
            Add(model, $"Disco {index++}",
                $"{disk.Model} · {Formatting.Bytes(disk.SizeBytes)} · {disk.InterfaceType ?? disk.BusType} · " +
                $"salud: {disk.HealthStatus ?? disk.OperationalStatus ?? "sin datos"}");
        }

        foreach (var volume in data?.Volumes ?? new List<VolumeInfo>())
        {
            Add(model, $"Unidad {volume.DriveLetter}",
                $"{volume.Label} · {volume.FileSystem} · {Formatting.Bytes(volume.SizeBytes)} · " +
                $"libre {Formatting.Bytes(volume.FreeBytes)} · uso {Formatting.Percent(volume.UsedPercent)}");
        }

        Add(model, "Nota de salud", data?.HealthQueryNote);
        AddChecks(model, category);
        return model;
    }

    private static CategoryViewModel BuildNetwork(InventorySnapshot snapshot)
    {
        var category = snapshot.Network;
        var data = category.Data;
        var active = data?.Adapters.Count(adapter => adapter.IPv4Addresses.Count > 0) ?? 0;
        var model = new CategoryViewModel
        {
            Id = category.Id,
            Title = category.Title,
            Status = category.Status,
            Summary = $"{data?.Adapters.Count ?? 0} interfaz(es) · {active} con IPv4"
        };

        foreach (var adapter in data?.Adapters
                     .OrderByDescending(adapter => adapter.IPv4Addresses.Count > 0)
                     .ThenBy(adapter => adapter.Name) ?? Enumerable.Empty<NetworkAdapterInfo>())
        {
            Add(model, adapter.Name ?? "Interfaz",
                $"{adapter.Status} · {adapter.AdapterType} · {Formatting.LinkSpeed(adapter.LinkSpeedBps)} · " +
                $"MAC {adapter.MacAddress} · IPv4 {Join(adapter.IPv4Addresses.ToArray())} · " +
                $"DHCP {Formatting.YesNo(adapter.DhcpEnabled)}");
        }

        AddChecks(model, category);
        return model;
    }

    private static CategoryViewModel BuildSoftware(InventorySnapshot snapshot)
    {
        var category = snapshot.Software;
        var data = category.Data;
        var model = new CategoryViewModel
        {
            Id = category.Id,
            Title = category.Title,
            Status = category.Status,
            Summary = $"{data?.TotalCount ?? 0} programa(s) registrado(s)",
            ProgramsTitle = "Programas instalados"
        };

        Add(model, "Total", data?.TotalCount.ToString());
        Add(model, "Ámbito del sistema", data?.SystemScopeCount?.ToString());
        Add(model, "Ámbito del usuario", data?.UserScopeCount?.ToString());
        foreach (var limitation in data?.Limitations ?? new List<string>())
        {
            Add(model, "Limitación", limitation);
        }

        foreach (var program in data?.Programs
                     .OrderBy(program => program.Name, StringComparer.CurrentCultureIgnoreCase)
                 ?? Enumerable.Empty<InstalledProgramInfo>())
        {
            model.Programs.Add(new ProgramRow(
                program.Name ?? "—",
                program.Version,
                program.Publisher,
                Formatting.Date(program.InstallDate),
                program.Scope));
        }

        AddChecks(model, category);
        return model;
    }

    private static CategoryViewModel BuildOffice(InventorySnapshot snapshot)
    {
        var category = snapshot.Office;
        var data = category.Data;
        var model = new CategoryViewModel
        {
            Id = category.Id,
            Title = category.Title,
            Status = category.Status,
            Summary = data?.Installed switch
            {
                true => $"{data.Installations.Count} instalación(es)",
                false => "No detectado",
                null => "No verificable"
            },
            ProgramsTitle = "Instalaciones detectadas"
        };

        Add(model, "Instalado", data?.Installed switch { true => "Sí", false => "No", null => "No verificable" });
        foreach (var installation in data?.Installations ?? new List<OfficeInstallationInfo>())
        {
            Add(model, installation.ProductName ?? "Office",
                $"versión {installation.Version} · {installation.Architecture} · " +
                $"{installation.InstallType} · {installation.UpdateChannel}");
        }

        foreach (var license in data?.Licenses ?? new List<OfficeLicenseInfo>())
        {
            Add(model, "Licencia", $"{license.ProductName} · {license.LicenseStatusText}");
        }

        Add(model, "Nota", data?.DetectionNote);
        Add(model, "Nota", data?.SubscriptionNote);

        AddChecks(model, category);
        return model;
    }

    private static CategoryViewModel BuildSecurity(InventorySnapshot snapshot)
    {
        var category = snapshot.Security;
        var data = category.Data;
        var model = new CategoryViewModel
        {
            Id = category.Id,
            Title = category.Title,
            Status = category.Status,
            Summary = data is null
                ? string.Empty
                : $"{data.Antivirus.Count} antivirus · firewall " +
                  $"{(data.Firewall.Any(profile => profile.Enabled == false) ? "con perfiles inactivos" : "revisado")}"
        };

        foreach (var antivirus in data?.Antivirus ?? new List<AntivirusInfo>())
        {
            Add(model, "Antivirus",
                $"{antivirus.DisplayName} · activo: {Formatting.YesNo(antivirus.Enabled)} · " +
                $"firmas: {Formatting.YesNo(antivirus.DefinitionsUpToDate, "actualizadas", "desactualizadas")}");
        }

        if (data?.Defender is { } defender)
        {
            Add(model, "Microsoft Defender",
                $"antivirus: {Formatting.YesNo(defender.AntivirusEnabled)} · " +
                $"tiempo real: {Formatting.YesNo(defender.RealTimeProtectionEnabled)} · " +
                $"firmas: {defender.SignatureVersion} ({defender.SignatureAgeDays} día(s))");
        }

        foreach (var profile in data?.Firewall ?? new List<FirewallProfileInfo>())
        {
            Add(model, $"Firewall {profile.Profile}",
                profile.Enabled == true ? "Activo" : profile.Enabled == false ? "Inactivo" : "Sin datos");
        }

        foreach (var volume in data?.BitLocker ?? new List<BitLockerVolumeInfo>())
        {
            Add(model, $"BitLocker {volume.DriveLetter}",
                $"{volume.ProtectionText} · {volume.ConversionText} · {volume.EncryptionMethod}");
        }

        Add(model, "TPM", data?.Tpm is null
            ? "No verificable"
            : $"presente · valor inicial habilitado: {Formatting.YesNo(data.Tpm.Enabled)} · " +
              $"valor inicial activado: {Formatting.YesNo(data.Tpm.Activated)} · {data.Tpm.SpecVersion}");
        Add(model, "Secure Boot", data?.SecureBoot is null
            ? "No verificable"
            : $"{(data.SecureBoot.Enabled == true ? "Activado" : "Desactivado")} · " +
              $"firmware {data.SecureBoot.FirmwareType}");
        AddChecks(model, category);
        return model;
    }

    private static void Add(CategoryViewModel model, string label, string? value, bool mono = false)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        model.Rows.Add(new DetailRow(label, value, mono));
    }

    private static void AddChecks(CategoryViewModel model, ICategoryResult category)
    {
        foreach (var check in category.Checks)
        {
            model.Checks.Add(new CheckRow(category.Title, check));
        }
    }

    private static string Join(params string?[] values)
    {
        var text = string.Join(" · ", values.Where(value => !string.IsNullOrWhiteSpace(value)));
        return text;
    }

    private static string? JoinBuild(string? version, string? build, int? revision)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(version))
        {
            parts.Add(version.Trim());
        }

        if (!string.IsNullOrWhiteSpace(build))
        {
            parts.Add(build.Trim());
        }

        if (revision is { } number)
        {
            parts.Add(number.ToString());
        }

        return parts.Count == 0 ? null : string.Join(" · ", parts);
    }
}
