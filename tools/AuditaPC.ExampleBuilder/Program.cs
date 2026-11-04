using System.Drawing;
using System.Drawing.Imaging;
using AuditaPC.Core.Matching;
using AuditaPC.Core.Model;
using AuditaPC.Core.Reporting;
using AuditaPC.Core.Storage;

// Genera una auditoría completamente ficticia para documentación y pruebas visuales.
// No consulta el equipo real, no contiene datos de clientes y no se publica sola.

var output = args.Length > 0
    ? Path.GetFullPath(args[0])
    : Path.GetFullPath(Path.Combine("examples", "auditoria-ficticia"));

const string markerName = ".auditapc-example";
var markerPath = Path.Combine(output, markerName);

if (Directory.Exists(output))
{
    if (!File.Exists(markerPath))
    {
        Console.Error.WriteLine(
            $"La carpeta {output} ya existe y no parece generada por esta herramienta. " +
            "Elige otra ruta para no borrar datos.");
        return 1;
    }

    Directory.Delete(output, recursive: true);
}

Directory.CreateDirectory(output);
File.WriteAllText(
    markerPath,
    "Ejemplo ficticio generado por AuditaPC.ExampleBuilder. No contiene datos reales.\n");

var repository = new AuditRepository();
using var workspace = repository.CreateAudit(
    output,
    "Servicios Integrales Cordillera SpA",
    "Técnico de prueba",
    "Levantamiento técnico demostrativo. Empresa, personas, equipos y datos completamente ficticios.",
    new DateOnly(2026, 11, 4));

workspace.Manifest.VisitSummary = "Visita técnica ficticia para levantar inventario, condiciones de funcionamiento y necesidades de continuidad operacional.";
workspace.Manifest.ConnectivityNotes = "La sede dispone de red cableada en administración y Wi-Fi corporativo en áreas comunes. No se habilitó acceso a Internet durante el levantamiento.";
workspace.Manifest.ModulesNotes = "Se revisaron módulos de gestión, contabilidad, bodega y herramientas de soporte. La prueba funcional de cada módulo queda sujeta a validación con el responsable.";
workspace.Manifest.ConditionNotes = "Los equipos ficticios se presentan operativos. Se registran diferencias de memoria, almacenamiento y antigüedad para demostrar la comparación entre activos.";
workspace.Manifest.FindingsNotes = "Un equipo requiere elevación para consultar BitLocker. Se recomienda revisar el espacio libre del equipo de recepción y confirmar la vigencia contractual de las suscripciones Microsoft 365.";
workspace.Manifest.ActionPlanNotes = "Verificar la configuración de BitLocker con una sesión elevada, revisar capacidad disponible y confirmar las licencias con el responsable de la cuenta Microsoft.";
workspace.Manifest.LimitationsNotes = "No se realizaron pruebas de rendimiento, acceso a Internet, validación contractual de licencias ni pruebas de negocio. Los datos son ficticios y no representan una empresa real.";
workspace.Manifest.OtherNotes = "Ejemplo creado para revisar la presentación del informe HTML y las exportaciones CSV/JSON.";
workspace.SaveManifest();

var reception = BuildReceptionComputer();
workspace.SaveComputer(reception);
workspace.SaveCapture(
    reception,
    BuildReceptionSnapshot(),
    "Captura ficticia de recepción.");
AddExamplePhoto(workspace, reception, "Etiqueta de activo de ejemplo");

var accounting = BuildAccountingComputer();
workspace.SaveComputer(accounting);
workspace.SaveCapture(
    accounting,
    BuildAccountingSnapshot(16),
    "Primera captura ficticia de contabilidad.");
workspace.SaveCapture(
    accounting,
    BuildAccountingSnapshot(32),
    "Segunda captura ficticia: se amplió la memoria RAM.");

var support = BuildSupportComputer();
workspace.SaveComputer(support);
workspace.SaveCapture(support, BuildSupportSnapshot(), "Captura ficticia de notebook de soporte.");

var warehouse = BuildWarehouseComputer();
workspace.SaveComputer(warehouse);
workspace.SaveCapture(warehouse, BuildWarehouseSnapshot(), "Captura ficticia de estación de bodega.");

var server = BuildManualComputer();
workspace.SaveComputer(server);

var computers = workspace.LoadComputers();
var bundle = new ReportWriter().Generate(workspace, computers);

Console.WriteLine("Ejemplo ficticio generado.");
Console.WriteLine($"Auditoría: {workspace.FolderPath}");
Console.WriteLine($"Equipos: {computers.Count} (4 con captura automática y 1 ficha manual)");
Console.WriteLine($"Informe HTML: {bundle.HtmlPath}");
Console.WriteLine($"CSV: {bundle.SummaryCsvPath}");
Console.WriteLine($"JSON: {bundle.JsonPath}");
return 0;

static ComputerRecord BuildReceptionComputer() => new()
{
    DisplayName = "PC-RECEPCION-01",
    Fingerprint = FingerprintFactory.FromSnapshot(BuildReceptionSnapshot()),
    Manual = new ManualRecord
    {
        Technician = "Técnico de prueba",
        AssetCode = "ACT-FICT-001",
        Responsible = "Recepción",
        Location = "Planta baja, mostrador",
        Notes = "Equipo ficticio usado para demostrar el informe. No corresponde a una persona ni empresa real.",
        IssuesFound = "El disco reporta salud correcta; se recomienda revisar el espacio libre.",
        Recommendations = "Programar limpieza de archivos temporales y revisar actualizaciones.",
        UpdatedAt = new DateTimeOffset(2026, 1, 15, 10, 30, 0, TimeSpan.Zero)
    }
};

static ComputerRecord BuildAccountingComputer() => new()
{
    DisplayName = "PC-CONTABILIDAD-02",
    Fingerprint = FingerprintFactory.FromSnapshot(BuildAccountingSnapshot(16)),
    Manual = new ManualRecord
    {
        Technician = "Técnico de prueba",
        AssetCode = "ACT-FICT-002",
        Responsible = "Contabilidad",
        Location = "Primer piso, oficina 3",
        Notes = "Equipo ficticio con dos capturas para mostrar el historial.",
        Interventions = "Ampliación de memoria simulada entre la primera y la segunda captura.",
        UpdatedAt = new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero)
    }
};

static ComputerRecord BuildSupportComputer() => new()
{
    DisplayName = "NOTEBOOK-SOPORTE-03",
    Fingerprint = FingerprintFactory.FromSnapshot(BuildSupportSnapshot()),
    Manual = new ManualRecord
    {
        Technician = "Técnico de prueba",
        AssetCode = "ACT-FICT-003",
        Responsible = "Soporte TI",
        Location = "Puesto de soporte, segundo piso",
        Notes = "Notebook ficticio utilizado para representar un equipo móvil de soporte.",
        Interventions = "Se verificaron herramientas de soporte y conectividad Wi-Fi local.",
        Recommendations = "Mantener las herramientas de soporte actualizadas y revisar la autonomía de batería en la siguiente visita.",
        UpdatedAt = new DateTimeOffset(2026, 11, 4, 13, 0, 0, TimeSpan.Zero)
    }
};

static ComputerRecord BuildWarehouseComputer() => new()
{
    DisplayName = "PC-BODEGA-04",
    Fingerprint = FingerprintFactory.FromSnapshot(BuildWarehouseSnapshot()),
    Manual = new ManualRecord
    {
        Technician = "Técnico de prueba",
        AssetCode = "ACT-FICT-004",
        Responsible = "Encargado de bodega",
        Location = "Bodega principal",
        Notes = "Estación ficticia de bodega. La red local se encuentra desconectada al momento de la captura.",
        IssuesFound = "No se detectó una instalación de Microsoft Office en esta estación de ejemplo.",
        PendingWork = "Confirmar con el responsable si el equipo requiere una suite ofimática.",
        UpdatedAt = new DateTimeOffset(2026, 11, 4, 13, 30, 0, TimeSpan.Zero)
    }
};

static ComputerRecord BuildManualComputer() => new()
{
    DisplayName = "SERVER-ARCHIVOS-05 (ficha manual)",
    Manual = new ManualRecord
    {
        Technician = "Técnico de prueba",
        AssetCode = "ACT-FICT-005",
        Responsible = "Administración",
        Location = "Sala técnica",
        Notes = "Ficha creada manualmente para demostrar que un equipo puede registrarse sin captura automática.",
        PendingWork = "Pendiente de captura automática en una próxima visita.",
        UpdatedAt = new DateTimeOffset(2026, 11, 4, 14, 0, 0, TimeSpan.Zero)
    }
};

static InventorySnapshot BuildSupportSnapshot()
{
    var snapshot = BaseSnapshot("NOTEBOOK-SOPORTE-03", "Fabricante Ficticio", "Modelo Portátil 700",
        "SERIE-FICT-0003", "UUID-FICT-0003", 16);
    snapshot.Identity.Data!.BiosAssetTag = "ACT-FICT-003";
    snapshot.Identity.Data.ChassisType = "Notebook";
    snapshot.Network.Data!.Adapters[0].Name = "Wi-Fi corporativo de ejemplo";
    snapshot.Network.Data.Adapters[0].AdapterType = "Wi-Fi";
    snapshot.Network.Data.Adapters[0].IPv4Addresses.Clear();
    snapshot.Network.Data.Adapters[0].IPv4Addresses.Add("192.0.2.30");
    snapshot.Software.Data!.Programs.Add(new InstalledProgramInfo
    {
        Name = "Herramienta de Soporte Remoto (ficticia)",
        Version = "4.2",
        Publisher = "Editor Ficticio",
        InstallDate = new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero),
        Scope = "Sistema"
    });
    snapshot.Software.Data.TotalCount = snapshot.Software.Data.Programs.Count;
    return snapshot;
}

static InventorySnapshot BuildWarehouseSnapshot()
{
    var snapshot = BaseSnapshot("PC-BODEGA-04", "Fabricante Ficticio", "Modelo Escritorio 80",
        "SERIE-FICT-0004", "UUID-FICT-0004", 8);
    snapshot.Identity.Data!.BiosAssetTag = "ACT-FICT-004";
    snapshot.Identity.Data.ChassisType = "Desktop";
    snapshot.OperatingSystem.Data!.ProductName = "Windows 10 Pro";
    snapshot.OperatingSystem.Data.FriendlyName = "Windows 10 Pro 22H2";
    snapshot.OperatingSystem.Data.Edition = "Professional";
    snapshot.OperatingSystem.Data.DisplayVersion = "22H2";
    snapshot.OperatingSystem.Data.Version = "10.0.19045";
    snapshot.OperatingSystem.Data.BuildNumber = "19045";
    snapshot.OperatingSystem.Data.UpdateBuildRevision = 5000;
    snapshot.Network.Data!.Adapters[0].Status = "Desconectado";
    snapshot.Network.Data.Adapters[0].IPv4Addresses.Clear();
    snapshot.Office.Data!.Installed = false;
    snapshot.Office.Data.DetectionNote = "No se detectó una instalación de Office en este equipo ficticio.";
    snapshot.Office.Data.Installations.Clear();
    snapshot.Office.Data.Licenses.Clear();
    snapshot.Office.Checks.Clear();
    snapshot.Office.Checks.Add(CheckRecord.NotDetected(
        "office.installed", "Instalación de Office", "No se detectó Office en la captura ficticia."));
    snapshot.Office.Status = CheckStatus.NotDetected;
    snapshot.Software.Data!.Programs.Add(new InstalledProgramInfo
    {
        Name = "Sistema de Inventario de Bodega (ficticio)",
        Version = "2.8",
        Publisher = "Soluciones Ficticias",
        InstallDate = new DateTimeOffset(2025, 7, 15, 0, 0, 0, TimeSpan.Zero),
        Scope = "Sistema"
    });
    snapshot.Software.Data.TotalCount = snapshot.Software.Data.Programs.Count;
    return snapshot;
}

static InventorySnapshot BuildReceptionSnapshot()
{
    var snapshot = BaseSnapshot("PC-RECEPCION-01", "Fabricante Ficticio", "Modelo Escritorio 100",
        "SERIE-FICT-0001", "UUID-FICT-0001", 8);
    snapshot.Identity.Data!.BiosAssetTag = "ACT-FICT-001";
    snapshot.Identity.Data.ChassisType = "Desktop";
    snapshot.OperatingSystem.Data!.FriendlyName = "Windows 11 Pro 24H2";
    snapshot.OperatingSystem.Data.ProductName = "Windows 11 Pro";
    snapshot.OperatingSystem.Data.Edition = "Professional";
    snapshot.OperatingSystem.Data.DisplayVersion = "24H2";
    snapshot.OperatingSystem.Data.BuildNumber = "26100";
    snapshot.OperatingSystem.Data.UpdateBuildRevision = 1000;
    snapshot.OperatingSystem.Data.SystemLocale = "es-CL";
    snapshot.Software.Data!.Programs.Add(new InstalledProgramInfo
    {
        Name = "Navegador de Ejemplo",
        Version = "120.0",
        Publisher = "Editor Ficticio",
        InstallDate = new DateTimeOffset(2025, 11, 1, 0, 0, 0, TimeSpan.Zero),
        Scope = "Sistema"
    });
    snapshot.Software.Data.TotalCount = snapshot.Software.Data.Programs.Count;
    snapshot.Storage.Data!.Volumes[0].FreeBytes = 90L * 1024 * 1024 * 1024;
    snapshot.Storage.Data.Volumes[0].UsedPercent = 82;
    return snapshot;
}

static InventorySnapshot BuildAccountingSnapshot(int ramGb)
{
    var snapshot = BaseSnapshot("PC-CONTABILIDAD-02", "Fabricante Ficticio", "Modelo Portátil 500",
        "SERIE-FICT-0002", "UUID-FICT-0002", ramGb);
    snapshot.Identity.Data!.BiosAssetTag = "ACT-FICT-002";
    snapshot.Identity.Data.ChassisType = "Notebook";
    snapshot.OperatingSystem.Data!.FriendlyName = "Windows 11 Pro 24H2";
    snapshot.OperatingSystem.Data.ProductName = "Windows 11 Pro";
    snapshot.OperatingSystem.Data.Edition = "Professional";
    snapshot.OperatingSystem.Data.DisplayVersion = "24H2";
    snapshot.OperatingSystem.Data.BuildNumber = "26100";
    snapshot.OperatingSystem.Data.UpdateBuildRevision = 1000;
    snapshot.OperatingSystem.Data.SystemLocale = "es-CL";
    snapshot.Software.Data!.Programs.Add(new InstalledProgramInfo
    {
        Name = "Suite Contable de Ejemplo",
        Version = "9.4",
        Publisher = "Software Ficticio",
        InstallDate = new DateTimeOffset(2025, 9, 10, 0, 0, 0, TimeSpan.Zero),
        Scope = "Sistema"
    });
    snapshot.Software.Data.TotalCount = snapshot.Software.Data.Programs.Count;
    snapshot.Security.Data!.BitLocker.Clear();
    snapshot.Security.Checks.Add(CheckRecord.RequiresElevation(
        "security.bitlocker", "BitLocker", "Requiere ejecutar AuditaPC como administrador para consultar el estado."));
    snapshot.Security.Status = CheckStatus.Partial;
    return snapshot;
}

static InventorySnapshot BaseSnapshot(
    string computerName,
    string manufacturer,
    string model,
    string serial,
    string uuid,
    int ramGb)
{
    return new InventorySnapshot
    {
        Environment = new CaptureEnvironment
        {
            MachineName = computerName,
            UserName = "tecnico.ejemplo",
            UserDomain = "EJEMPLO",
            IsElevated = false,
            Is64BitOperatingSystem = true,
            Is64BitProcess = true,
            OsDescription = "Microsoft Windows 11 Pro (ficticio)",
            ProcessArchitecture = "X64",
            StartedAt = new DateTimeOffset(2026, 1, 15, 9, 0, 0, TimeSpan.Zero),
            FinishedAt = new DateTimeOffset(2026, 1, 15, 9, 2, 0, TimeSpan.Zero)
        },
        Identity = new CategoryResult<IdentityData>
        {
            Id = "identity",
            Title = "Identificación",
            Status = CheckStatus.Verified,
            Data = new IdentityData
            {
                ComputerName = computerName,
                Manufacturer = manufacturer,
                Model = model,
                Architecture = "x64",
                FirmwareType = "UEFI",
                BiosVendor = "Firmware Ficticio",
                BiosVersion = "1.0.0",
                BiosReleaseDate = new DateTimeOffset(2025, 6, 1, 0, 0, 0, TimeSpan.Zero),
                BiosSerial = serial,
                SystemUuid = uuid,
                BaseboardManufacturer = "Placa Ficticia",
                BaseboardProduct = "Base de Ejemplo",
                BaseboardSerial = "PLACA-" + serial,
                MachineGuid = "GUID-" + serial,
                Domain = "ejemplo.local",
                PartOfDomain = true,
                CurrentUser = "tecnico.ejemplo",
                CurrentUserDomain = "EJEMPLO",
                IsElevated = false,
                IsVirtualMachine = false
            },
            Checks =
            {
                CheckRecord.Verified("identity.name", "Nombre del equipo", computerName),
                CheckRecord.Verified("identity.serial", "Número de serie", serial),
                CheckRecord.Verified("identity.domain", "Dominio", "ejemplo.local")
            }
        },
        OperatingSystem = new CategoryResult<OsData>
        {
            Id = "os",
            Title = "Sistema operativo",
            Status = CheckStatus.Verified,
            Data = new OsData
            {
                ProductName = "Windows 11 Pro",
                Edition = "Professional",
                DisplayVersion = "24H2",
                Version = "10.0.26100",
                BuildNumber = "26100",
                UpdateBuildRevision = 1000,
                Architecture = "64 bits",
                InstallDate = new DateTimeOffset(2025, 3, 1, 0, 0, 0, TimeSpan.Zero),
                LastBootTime = new DateTimeOffset(2026, 1, 15, 8, 0, 0, TimeSpan.Zero),
                InstallationType = "Client",
                SystemDrive = "C:",
                SystemLocale = "es-CL",
                FriendlyName = "Windows 11 Pro 24H2",
                License = new WindowsLicenseInfo
                {
                    ProductName = "Windows 11 Pro",
                    LicenseStatusText = "Con licencia",
                    IsLicensed = true,
                    Channel = "Retail"
                }
            },
            Checks =
            {
                CheckRecord.Verified("os.version", "Versión de Windows", "Windows 11 Pro 24H2"),
                CheckRecord.NotTestedOffline("os.pending-updates", "Actualizaciones pendientes",
                    "No se consultó Windows Update durante el levantamiento; requiere Internet y está fuera de la captura local.")
            }
        },
        Hardware = new CategoryResult<HardwareData>
        {
            Id = "hardware",
            Title = "Procesador y memoria",
            Status = CheckStatus.Verified,
            Data = new HardwareData
            {
                Processors =
                {
                    new CpuInfo
                    {
                        Name = "Procesador Ficticio de 8 núcleos",
                        Cores = 8,
                        LogicalProcessors = 16,
                        MaxClockMhz = 3200
                    }
                },
                TotalMemoryBytes = ramGb * 1024L * 1024 * 1024,
                MemorySlotCount = 2,
                MemoryMaxCapacityBytes = 64L * 1024 * 1024 * 1024,
                MemoryModules =
                {
                    new MemoryModuleInfo
                    {
                        CapacityBytes = ramGb * 1024L * 1024 * 1024,
                        SpeedMhz = 3200,
                        MemoryType = "DDR4",
                        Manufacturer = "Memoria Ficticia",
                        PartNumber = $"MEM-FICT-{ramGb}"
                    }
                }
            },
            Checks =
            {
                CheckRecord.Verified("hardware.cpu", "Procesador", "Procesador Ficticio de 8 núcleos"),
                CheckRecord.Verified("hardware.ram", "Memoria RAM", $"{ramGb} GB")
            }
        },
        Storage = new CategoryResult<StorageData>
        {
            Id = "storage",
            Title = "Almacenamiento",
            Status = CheckStatus.Verified,
            Data = new StorageData
            {
                PhysicalDisks =
                {
                    new PhysicalDiskInfo
                    {
                        Model = "Disco Ficticio SSD",
                        SerialNumber = "DISCO-" + serial,
                        SizeBytes = 512L * 1024 * 1024 * 1024,
                        InterfaceType = "NVMe",
                        HealthStatus = "Healthy"
                    }
                },
                Volumes =
                {
                    new VolumeInfo
                    {
                        DriveLetter = "C:",
                        Label = "Sistema",
                        FileSystem = "NTFS",
                        SizeBytes = 512L * 1024 * 1024 * 1024,
                        FreeBytes = 180L * 1024 * 1024 * 1024,
                        UsedPercent = 65,
                        IsSystemVolume = true
                    }
                },
                HealthQueryAvailable = true
            },
            Checks =
            {
                CheckRecord.Verified("storage.disk", "Disco físico", "Disco Ficticio SSD"),
                CheckRecord.Verified("storage.volume", "Volumen del sistema", "C: NTFS")
            }
        },
        Network = new CategoryResult<NetworkData>
        {
            Id = "network",
            Title = "Red local",
            Status = CheckStatus.Verified,
            Data = new NetworkData
            {
                PhysicalAdapterCount = 1,
                Adapters =
                {
                    new NetworkAdapterInfo
                    {
                        Name = "Ethernet de ejemplo",
                        AdapterType = "Ethernet",
                        MacAddress = "02-00-00-00-00-02",
                        Status = "Conectado",
                        LinkSpeedBps = 1_000_000_000,
                        IPv4Addresses = { "192.0.2.20" },
                        DhcpEnabled = true,
                        Gateways = { "192.0.2.1" },
                        DnsServers = { "192.0.2.53" }
                    }
                }
            },
            Checks =
            {
                CheckRecord.Verified("network.adapter", "Adaptador de red", "Ethernet de ejemplo"),
                CheckRecord.Verified("network.ipv4", "Dirección IPv4", "192.0.2.20")
            }
        },
        Software = new CategoryResult<SoftwareData>
        {
            Id = "software",
            Title = "Software instalado",
            Status = CheckStatus.Verified,
            Data = new SoftwareData
            {
                Programs =
                {
                    new InstalledProgramInfo
                    {
                        Name = "Programa de Ejemplo",
                        Version = "1.2.3",
                        Publisher = "Editor Ficticio",
                        InstallDate = new DateTimeOffset(2025, 5, 1, 0, 0, 0, TimeSpan.Zero),
                        Scope = "Sistema"
                    }
                },
                TotalCount = 1,
                SystemScopeCount = 1,
                UserScopeCount = 0
            },
            Checks =
            {
                CheckRecord.Verified("software.count", "Programas instalados", "Programas ficticios"),
                CheckRecord.Verified("software.scope", "Ámbitos consultados", "Sistema y usuario")
            }
        },
        Office = new CategoryResult<OfficeData>
        {
            Id = "office",
            Title = "Microsoft Office",
            Status = CheckStatus.Verified,
            Data = new OfficeData
            {
                Installed = true,
                DetectionNote = "La vigencia contractual de Microsoft 365 no puede verificarse sin conexión.",
                Installations =
                {
                    new OfficeInstallationInfo
                    {
                        ProductName = "Microsoft 365 Apps (ejemplo)",
                        Version = "16.0.1",
                        Architecture = "x64",
                        InstallType = "ClickToRun",
                        UpdateChannel = "Current Channel"
                    }
                },
                Licenses =
                {
                    new OfficeLicenseInfo
                    {
                        ProductName = "Microsoft 365 Apps (ejemplo)",
                        LicenseStatusText = "No verificable sin conexión"
                    }
                }
            },
            Checks =
            {
                CheckRecord.Detected("office.installed", "Instalación de Office", "Microsoft 365 Apps (ejemplo)"),
                CheckRecord.NotTestedOffline("office.subscription", "Vigencia de suscripción Microsoft 365", "No se consultó la vigencia contractual porque la comprobación en línea está desactivada."),
                CheckRecord.NotTestedOffline("office.license.connectivity", "Conectividad con licenciamiento de Office", "La comprobación en línea está desactivada."),
                CheckRecord.KnownLimitation("office.coverage", "Cobertura de la detección", "La activación local no confirma titularidad ni vigencia contractual.")
            }
        },
        Security = new CategoryResult<SecurityData>
        {
            Id = "security",
            Title = "Seguridad de Windows",
            Status = CheckStatus.Verified,
            Data = new SecurityData
            {
                Antivirus =
                {
                    new AntivirusInfo
                    {
                        DisplayName = "Antivirus de Ejemplo",
                        Enabled = true,
                        DefinitionsUpToDate = true,
                        Source = "SecurityCenter2 (ficticio)"
                    }
                },
                Defender = new DefenderStatusInfo
                {
                    AntivirusEnabled = true,
                    RealTimeProtectionEnabled = true,
                    SignatureVersion = "1.0.0",
                    SignatureAgeDays = 0
                },
                Firewall =
                {
                    new FirewallProfileInfo { Profile = "Dominio", Enabled = true },
                    new FirewallProfileInfo { Profile = "Privado", Enabled = true },
                    new FirewallProfileInfo { Profile = "Público", Enabled = true }
                },
                Tpm = new TpmInfo
                {
                    Present = true,
                    Enabled = true,
                    Activated = true,
                    SpecVersion = "2.0"
                },
                SecureBoot = new SecureBootInfo
                {
                    Enabled = true,
                    FirmwareType = "UEFI"
                }
            },
            Checks =
            {
                CheckRecord.Verified("security.av", "Antivirus activo", "Antivirus de Ejemplo"),
                CheckRecord.Verified("security.firewall", "Firewall", "Todos los perfiles activos"),
                CheckRecord.Verified("security.secureboot", "Secure Boot", "Activado en firmware UEFI")
            }
        }
    };
}

static void AddExamplePhoto(AuditWorkspace workspace, ComputerRecord computer, string description)
{
    var temp = Path.Combine(Path.GetTempPath(), "auditapc-example-" + Guid.NewGuid().ToString("N") + ".png");
    try
    {
        using (var bitmap = new Bitmap(640, 360))
        {
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.Clear(Color.FromArgb(28, 36, 48));
                using var accent = new SolidBrush(Color.FromArgb(15, 107, 99));
                graphics.FillRectangle(accent, 0, 300, 640, 60);
                using var titleFont = new Font("Segoe UI", 30, FontStyle.Bold, GraphicsUnit.Pixel);
                using var bodyFont = new Font("Segoe UI", 18, FontStyle.Regular, GraphicsUnit.Pixel);
                using var white = new SolidBrush(Color.White);
                using var soft = new SolidBrush(Color.FromArgb(200, 220, 225, 230));
                graphics.DrawString("ETIQUETA DE EJEMPLO", titleFont, white, 32, 70);
                graphics.DrawString("Datos completamente ficticios", bodyFont, soft, 32, 120);
                graphics.DrawString("ACT-FICT-001 · PC-RECEPCION-01", bodyFont, white, 32, 180);
                graphics.DrawString("Generado por AuditaPC.ExampleBuilder", bodyFont, white, 32, 316);
            }

            bitmap.Save(temp, ImageFormat.Png);
        }
        var photo = new PhotoStore().AddPhoto(workspace.FolderPath, computer.Id, temp, description);
        computer.Photos.Add(photo);
        workspace.SaveComputer(computer);
    }
    finally
    {
        if (File.Exists(temp))
        {
            File.Delete(temp);
        }
    }
}
