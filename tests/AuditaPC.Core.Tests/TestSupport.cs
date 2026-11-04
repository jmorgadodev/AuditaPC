using AuditaPC.Core.Model;

namespace AuditaPC.Core.Tests;

internal sealed class TempDirectory : IDisposable
{
    public TempDirectory()
    {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "auditapc-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public string Combine(params string[] parts) =>
        System.IO.Path.Combine(new[] { Path }.Concat(parts).ToArray());

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
        catch
        {
            // La limpieza de temporales no debe ocultar el resultado de la prueba.
        }
    }
}

internal static class TestData
{
    public static InventorySnapshot CreateSnapshot(string computerName = "EQUIPO-DEMO")
    {
        return new InventorySnapshot
        {
            Environment = new CaptureEnvironment
            {
                MachineName = computerName,
                UserName = "tecnico",
                UserDomain = "DEMO",
                IsElevated = false,
                Is64BitOperatingSystem = true,
                Is64BitProcess = true,
                OsDescription = "Microsoft Windows 11 Pro",
                ProcessArchitecture = "X64",
                StartedAt = new DateTimeOffset(2026, 1, 15, 9, 58, 0, TimeSpan.Zero),
                FinishedAt = new DateTimeOffset(2026, 1, 15, 10, 0, 0, TimeSpan.Zero)
            },
            Identity = new CategoryResult<IdentityData>
            {
                Id = "identity",
                Title = "Identificación",
                Status = CheckStatus.Verified,
                Data = new IdentityData
                {
                    ComputerName = computerName,
                    Manufacturer = "Fabricante Ficticio",
                    Model = "Modelo de Ejemplo 500",
                    ChassisType = "Notebook",
                    Architecture = "x64",
                    FirmwareType = "UEFI",
                    BiosVendor = "Firmware Ficticio",
                    BiosVersion = "1.0.0",
                    BiosReleaseDate = new DateTimeOffset(2025, 6, 1, 0, 0, 0, TimeSpan.Zero),
                    BiosSerial = "SERIE-DEMO-0001",
                    BiosAssetTag = "ACTIVO-BIOS-01",
                    SystemUuid = "11111111-2222-3333-4444-555555555555",
                    BaseboardManufacturer = "Placa Ficticia",
                    BaseboardProduct = "Base 100",
                    BaseboardSerial = "PLACA-DEMO-0001",
                    MachineGuid = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
                    Domain = "demo.local",
                    PartOfDomain = true,
                    CurrentUser = "tecnico",
                    CurrentUserDomain = "DEMO",
                    IsElevated = false,
                    IsVirtualMachine = false
                },
                Checks =
                {
                    CheckRecord.Verified("identity.name", "Nombre del equipo", computerName),
                    CheckRecord.Verified("identity.serial", "Número de serie", "SERIE-DEMO-0001")
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
                    InstallDate = new DateTimeOffset(2025, 2, 10, 0, 0, 0, TimeSpan.Zero),
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
                    CheckRecord.NotTestedOffline("os.updates", "Actualizaciones pendientes", "No se consultó Windows Update sin conexión.")
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
                            Name = "Procesador Ficticio 8 núcleos",
                            Cores = 8,
                            LogicalProcessors = 16,
                            MaxClockMhz = 3200
                        }
                    },
                    TotalMemoryBytes = 16L * 1024 * 1024 * 1024,
                    MemorySlotCount = 2,
                    MemoryMaxCapacityBytes = 64L * 1024 * 1024 * 1024,
                    MemoryModules =
                    {
                        new MemoryModuleInfo
                        {
                            CapacityBytes = 16L * 1024 * 1024 * 1024,
                            SpeedMhz = 3200,
                            MemoryType = "DDR4",
                            Manufacturer = "Memoria Ficticia",
                            PartNumber = "MEM-DEMO-16"
                        }
                    }
                },
                Checks =
                {
                    CheckRecord.Verified("hardware.cpu", "Procesador", "Procesador Ficticio 8 núcleos"),
                    CheckRecord.Verified("hardware.ram", "Memoria RAM", "16 GB")
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
                            SerialNumber = "DISCO-DEMO-0001",
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
                            FreeBytes = 200L * 1024 * 1024 * 1024,
                            UsedPercent = 61,
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
                            MacAddress = "02-00-00-00-00-01",
                            Status = "Conectado",
                            LinkSpeedBps = 1_000_000_000,
                            IPv4Addresses = { "192.0.2.10" },
                            DhcpEnabled = true,
                            Gateways = { "192.0.2.1" },
                            DnsServers = { "192.0.2.53" }
                        }
                    }
                },
                Checks =
                {
                    CheckRecord.Verified("network.adapter", "Adaptador de red", "Ethernet de ejemplo"),
                    CheckRecord.Verified("network.ipv4", "Dirección IPv4", "192.0.2.10")
                }
            },
            Software = new CategoryResult<SoftwareData>
            {
                Id = "software",
                Title = "Software instalado",
                Status = CheckStatus.Verified,
                Data = new SoftwareData
                {
                    TotalCount = 2,
                    SystemScopeCount = 2,
                    UserScopeCount = 0,
                    Programs =
                    {
                        new InstalledProgramInfo
                        {
                            Name = "Programa de Ejemplo",
                            Version = "1.2.3",
                            Publisher = "Editor Ficticio",
                            InstallDate = new DateTimeOffset(2025, 5, 1, 0, 0, 0, TimeSpan.Zero),
                            Scope = "Sistema"
                        },
                        new InstalledProgramInfo
                        {
                            Name = "Herramienta de Ejemplo",
                            Version = "4.5.6",
                            Publisher = "Otro Editor Ficticio",
                            InstallDate = new DateTimeOffset(2025, 7, 1, 0, 0, 0, TimeSpan.Zero),
                            Scope = "Sistema"
                        }
                    }
                },
                Checks =
                {
                    CheckRecord.Verified("software.count", "Programas instalados", "2 programas"),
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
                            ProductName = "Microsoft 365 Apps",
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
                            ProductName = "Microsoft 365 Apps",
                            LicenseStatusText = "No verificable sin conexión"
                        }
                    }
                },
                Checks =
                {
                    CheckRecord.Detected("office.installed", "Instalación de Office", "Microsoft 365 Apps"),
                    CheckRecord.NotTestedOffline("office.subscription", "Vigencia de suscripción Microsoft 365", "No se consultó la vigencia contractual sin conexión."),
                    CheckRecord.NotTestedOffline("office.license.connectivity", "Conectividad con licenciamiento de Office", "La comprobación en línea está desactivada."),
                    CheckRecord.KnownLimitation("office.coverage", "Cobertura de la detección", "La activación local no confirma titularidad ni vigencia contractual.")
                }
            },
            Security = new CategoryResult<SecurityData>
            {
                Id = "security",
                Title = "Seguridad de Windows",
                Status = CheckStatus.Partial,
                Data = new SecurityData
                {
                    Antivirus =
                    {
                        new AntivirusInfo
                        {
                            DisplayName = "Antivirus de Ejemplo",
                            Enabled = true,
                            DefinitionsUpToDate = true,
                            Source = "SecurityCenter2"
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
                    }
                },
                Checks =
                {
                    CheckRecord.Verified("security.av", "Antivirus activo", "Antivirus de Ejemplo"),
                    CheckRecord.Verified("security.firewall", "Firewall", "Todos los perfiles activos"),
                    CheckRecord.RequiresElevation("security.bitlocker", "BitLocker", "Requiere ejecutar AuditaPC como administrador para consultar el estado.")
                }
            }
        };
    }

    public static ComputerRecord CreateComputer(string name = "EQUIPO-DEMO")
    {
        var snapshot = CreateSnapshot(name);
        return new ComputerRecord
        {
            DisplayName = name,
            Fingerprint = AuditaPC.Core.Matching.FingerprintFactory.FromSnapshot(snapshot),
            Manual = new ManualRecord
            {
                Technician = "Técnico de prueba",
                AssetCode = "ACT-0001",
                Location = "Oficina de prueba",
                Notes = "Datos completamente ficticios para pruebas automatizadas."
            }
        };
    }
}
