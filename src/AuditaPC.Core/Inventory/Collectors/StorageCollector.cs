using AuditaPC.Core.Model;

namespace AuditaPC.Core.Inventory.Collectors;

public sealed class StorageCollector : IInventoryCollector
{
    public string Id => "storage";
    public string Title => "Almacenamiento";
    public TimeSpan Timeout => TimeSpan.FromSeconds(18);

    public Task<CollectorOutcome> CollectAsync(CollectorContext context)
    {
        var checks = new List<CheckRecord>();
        var data = new StorageData();
        var obtained = false;

        var (diskRows, diskError) = TryQuery("Win32_DiskDrive", TimeSpan.FromSeconds(9));
        if (diskRows.Count > 0)
        {
            foreach (var row in diskRows)
            {
                data.PhysicalDisks.Add(new PhysicalDiskInfo
                {
                    Model = Clean(row.GetString("Model")),
                    SerialNumber = Clean(row.GetString("SerialNumber")),
                    SizeBytes = row.GetLong("Size"),
                    InterfaceType = Clean(row.GetString("InterfaceType")),
                    MediaType = Clean(row.GetString("MediaType")),
                    FirmwareVersion = Clean(row.GetString("FirmwareRevision")),
                    OperationalStatus = Clean(row.GetString("Status")),
                    DiskIndex = row.GetInt("Index"),
                    HealthSource = "Win32_DiskDrive (estado operativo)"
                });
            }

            obtained = true;
            checks.Add(CheckRecord.Verified("storage.disks", "Discos físicos",
                $"{data.PhysicalDisks.Count} disco(s) · " +
                Formatting.Bytes(data.PhysicalDisks.Sum(disk => disk.SizeBytes ?? 0)),
                "WMI Win32_DiskDrive"));
        }
        else
        {
            checks.Add(diskError is null
                ? CheckRecord.NotDetected("storage.disks", "Discos físicos",
                    "Windows no devolvió discos físicos.", "WMI Win32_DiskDrive")
                : CollectorErrors.FromWmi("storage.disks", "Discos físicos", diskError));
        }

        var (volumeRows, volumeError) = TryQuery("Win32_LogicalDisk", TimeSpan.FromSeconds(8),
            "DriveType = 3");
        if (volumeRows.Count > 0)
        {
            var systemDrive = Environment.GetEnvironmentVariable("SystemDrive");
            foreach (var row in volumeRows)
            {
                var size = row.GetLong("Size");
                var free = row.GetLong("FreeSpace");
                data.Volumes.Add(new VolumeInfo
                {
                    DriveLetter = Clean(row.GetString("DeviceID")),
                    Label = Clean(row.GetString("VolumeName")),
                    FileSystem = Clean(row.GetString("FileSystem")),
                    SizeBytes = size,
                    FreeBytes = free,
                    UsedPercent = size is > 0 && free is not null
                        ? Math.Round((1d - (free.Value / (double)size.Value)) * 100d, 1)
                        : null,
                    IsSystemVolume = string.Equals(
                        Clean(row.GetString("DeviceID")), systemDrive,
                        StringComparison.OrdinalIgnoreCase),
                    DriveType = "Disco local"
                });
            }

            obtained = true;
            checks.Add(CheckRecord.Verified("storage.volumes", "Unidades lógicas",
                $"{data.Volumes.Count} unidad(es) local(es)",
                "WMI Win32_LogicalDisk"));
        }
        else
        {
            checks.Add(volumeError is null
                ? CheckRecord.NotDetected("storage.volumes", "Unidades lógicas",
                    "No se detectaron unidades locales.", "WMI Win32_LogicalDisk")
                : CollectorErrors.FromWmi("storage.volumes", "Unidades lógicas", volumeError));
        }

        ReadDiskHealth(data, checks);

        checks.Add(CheckRecord.NotApplicable(
            "storage.performance",
            "Pruebas de rendimiento",
            "AuditaPC no ejecuta benchmarks ni pruebas de estrés.",
            "Alcance de AuditaPC"));

        var result = new CategoryResult<StorageData>
        {
            Id = Id,
            Title = Title,
            Data = data,
            Checks = checks,
            Status = CategoryStatus.Aggregate(checks, obtained)
        };

        return Task.FromResult(new CollectorOutcome
        {
            Id = Id,
            Status = result.Status,
            Apply = snapshot => snapshot.Storage = result
        });
    }

    public void ApplyUnavailable(InventorySnapshot target, CheckStatus status, string message, string? errorType)
    {
        target.Storage = CategoryResult<StorageData>.Unavailable(Id, Title, status, message, errorType);
    }

    private static void ReadDiskHealth(StorageData data, List<CheckRecord> checks)
    {
        try
        {
            var rows = WmiQuery.Query(
                "MSFT_PhysicalDisk",
                namespacePath: @"root\Microsoft\Windows\Storage",
                timeout: TimeSpan.FromSeconds(8),
                selectColumns: "DeviceId, MediaType, BusType, HealthStatus, OperationalStatus, Size");

            if (rows.Count == 0)
            {
                data.HealthQueryAvailable = false;
                data.HealthQueryNote = "Windows no devolvió información de MSFT_PhysicalDisk.";
                checks.Add(CheckRecord.NotVerifiable("storage.health", "Salud de los discos",
                    data.HealthQueryNote, "WMI MSFT_PhysicalDisk"));
                return;
            }

            data.HealthQueryAvailable = true;
            if (data.PhysicalDisks.Count == 0)
            {
                data.HealthQueryNote =
                    "Se obtuvo información del proveedor de almacenamiento, pero no se pudo asociar " +
                    "con discos físicos detectados por Win32_DiskDrive.";
                checks.Add(CheckRecord.NotVerifiable("storage.health", "Salud de los discos",
                    data.HealthQueryNote, "WMI MSFT_PhysicalDisk / Win32_DiskDrive"));
                return;
            }

            var byIndex = new Dictionary<string, WmiRow>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in rows)
            {
                var deviceId = row.GetString("DeviceId");
                if (!string.IsNullOrWhiteSpace(deviceId))
                {
                    byIndex[deviceId.Trim()] = row;
                }
            }

            var unknown = 0;
            foreach (var disk in data.PhysicalDisks)
            {
                if (disk.DiskIndex is null
                    || !byIndex.TryGetValue(disk.DiskIndex.Value.ToString(), out var healthRow))
                {
                    unknown++;
                    continue;
                }

                disk.MediaType = MapMediaType(healthRow.GetInt("MediaType")) ?? disk.MediaType;
                disk.BusType = MapBusType(healthRow.GetInt("BusType"));
                disk.HealthStatus = MapHealth(healthRow.GetInt("HealthStatus"));
                var operational = healthRow.GetIntList("OperationalStatus");
                if (operational.Count > 0)
                {
                    disk.OperationalStatus = string.Join(", ", operational.Select(MapOperationalStatus));
                }

                disk.HealthSource = "WMI MSFT_PhysicalDisk";
                if (disk.HealthStatus is null || disk.HealthStatus == "Desconocido")
                {
                    unknown++;
                }
            }

            var problems = data.PhysicalDisks
                .Where(disk => disk.HealthStatus == "Con problemas"
                               || ContainsAny(disk.OperationalStatus, "Error", "Fallo predictivo"))
                .ToList();
            var warnings = data.PhysicalDisks
                .Where(disk => disk.HealthStatus == "Advertencia"
                               || ContainsAny(disk.OperationalStatus, "Degradado"))
                .ToList();

            if (problems.Count > 0)
            {
                var detail = $"{problems.Count} disco(s) informan un estado de salud problemático: " +
                             string.Join(", ", problems.Select(disk => disk.Model ?? $"Disco {disk.DiskIndex}"));
                if (unknown > 0)
                {
                    detail += $". Además, {unknown} disco(s) no tienen estado verificable.";
                }

                checks.Add(CheckRecord.Problem(
                    "storage.health", "Salud de los discos", detail, "WMI MSFT_PhysicalDisk"));
            }
            else if (warnings.Count > 0)
            {
                var detail = $"{warnings.Count} disco(s) informan advertencias: " +
                             string.Join(", ", warnings.Select(disk => disk.Model ?? $"Disco {disk.DiskIndex}"));
                if (unknown > 0)
                {
                    detail += $". Además, {unknown} disco(s) no tienen estado verificable.";
                }

                checks.Add(CheckRecord.Detected(
                    "storage.health", "Salud de los discos", detail, "WMI MSFT_PhysicalDisk"));
            }
            else if (unknown > 0)
            {
                checks.Add(CheckRecord.NotVerifiable("storage.health", "Salud de los discos",
                    $"{unknown} disco(s) sin estado de salud verificable. " +
                    "Windows puede no exponer el estado para algunas controladoras o dispositivos USB.",
                    "WMI MSFT_PhysicalDisk"));
            }
            else
            {
                checks.Add(CheckRecord.Verified("storage.health", "Salud de los discos",
                    "Windows informa estado correcto para todos los discos detectados.",
                    "WMI MSFT_PhysicalDisk"));
            }
        }
        catch (WmiQueryException ex)
        {
            data.HealthQueryAvailable = false;
            data.HealthQueryNote = ex.Message;
            checks.Add(CollectorErrors.FromWmi(
                "storage.health", "Salud de los discos", ex, notFoundIsNotApplicable: false));
        }
    }

    private static bool ContainsAny(string? value, params string[] terms) =>
        !string.IsNullOrWhiteSpace(value)
        && terms.Any(term => value.Contains(term, StringComparison.OrdinalIgnoreCase));

    private static (IReadOnlyList<WmiRow> Rows, WmiQueryException? Error) TryQuery(
        string className,
        TimeSpan timeout,
        string? where = null)
    {
        try
        {
            return (WmiQuery.Query(className, where: where, timeout: timeout), null);
        }
        catch (WmiQueryException ex)
        {
            return (Array.Empty<WmiRow>(), ex);
        }
    }

    private static string? MapMediaType(int? code) => code switch
    {
        0 => "No especificado",
        3 => "Disco duro (HDD)",
        4 => "Unidad de estado sólido (SSD)",
        5 => "Memoria de clase de almacenamiento (SCM)",
        _ => null
    };

    private static string? MapBusType(int? code) => code switch
    {
        1 => "SCSI",
        2 => "ATAPI",
        3 => "ATA",
        4 => "IEEE 1394",
        6 => "Fibre Channel",
        7 => "USB",
        8 => "RAID",
        9 => "iSCSI",
        10 => "SAS",
        11 => "SATA",
        12 => "SD",
        13 => "MMC",
        14 => "Virtual",
        15 => "Virtual respaldado por archivo",
        16 => "Storage Spaces",
        17 => "NVMe",
        18 => "SCM",
        19 => "UFS",
        _ => null
    };

    private static string? MapHealth(int? code) => code switch
    {
        0 => "Correcto",
        1 => "Advertencia",
        2 => "Con problemas",
        5 => "Desconocido",
        _ => null
    };

    private static string MapOperationalStatus(int code) => code switch
    {
        2 => "Correcto",
        3 => "Degradado",
        4 => "Con tensión",
        5 => "Fallo predictivo",
        6 => "Error",
        8 => "Iniciando",
        9 => "Deteniéndose",
        11 => "En servicio",
        12 => "Sin contacto",
        13 => "Comunicación perdida",
        15 => "Inactivo",
        17 => "Completado",
        18 => "Modo de bajo consumo",
        _ => $"Estado {code}"
    };

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
