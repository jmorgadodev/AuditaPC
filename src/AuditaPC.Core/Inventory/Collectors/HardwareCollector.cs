using AuditaPC.Core.Model;

namespace AuditaPC.Core.Inventory.Collectors;

public sealed class HardwareCollector : IInventoryCollector
{
    public string Id => "hardware";
    public string Title => "Procesador y memoria";
    public TimeSpan Timeout => TimeSpan.FromSeconds(16);

    public Task<CollectorOutcome> CollectAsync(CollectorContext context)
    {
        var checks = new List<CheckRecord>();
        var data = new HardwareData();
        var obtained = false;

        var (computerRows, computerError) = TryQuery("Win32_ComputerSystem", TimeSpan.FromSeconds(7));
        if (computerRows.Count > 0)
        {
            data.TotalMemoryBytes = computerRows[0].GetLong("TotalPhysicalMemory");
        }
        else if (computerError is not null)
        {
            checks.Add(CollectorErrors.FromWmi("hardware.total", "Memoria total", computerError));
        }

        var (processorRows, processorError) = TryQuery("Win32_Processor", TimeSpan.FromSeconds(8));
        if (processorRows.Count > 0)
        {
            foreach (var row in processorRows)
            {
                data.Processors.Add(new CpuInfo
                {
                    Name = Clean(row.GetString("Name")),
                    Manufacturer = Clean(row.GetString("Manufacturer")),
                    Cores = row.GetInt("NumberOfCores"),
                    LogicalProcessors = row.GetInt("NumberOfLogicalProcessors"),
                    MaxClockMhz = row.GetInt("MaxClockSpeed"),
                    Socket = Clean(row.GetString("SocketDesignation")),
                    L2CacheKb = row.GetInt("L2CacheSize"),
                    L3CacheKb = row.GetInt("L3CacheSize"),
                    ProcessorId = Clean(row.GetString("ProcessorId"))
                });
            }

            obtained = true;
            var summary = string.Join(" | ", data.Processors
                .Select(cpu => cpu.Name)
                .Where(name => !string.IsNullOrWhiteSpace(name)));
            checks.Add(CheckRecord.Verified("hardware.cpu", "Procesador",
                $"{summary} · {data.Processors.Sum(cpu => cpu.Cores ?? 0)} núcleos / " +
                $"{data.Processors.Sum(cpu => cpu.LogicalProcessors ?? 0)} hilos",
                "WMI Win32_Processor"));
        }
        else
        {
            checks.Add(processorError is null
                ? CheckRecord.NotVerifiable("hardware.cpu", "Procesador",
                    "Windows no devolvió datos de Win32_Processor.", "WMI Win32_Processor")
                : CollectorErrors.FromWmi("hardware.cpu", "Procesador", processorError));
        }

        var (memoryRows, memoryError) = TryQuery("Win32_PhysicalMemory", TimeSpan.FromSeconds(8));
        if (memoryRows.Count > 0)
        {
            foreach (var row in memoryRows)
            {
                var smbiosType = row.GetInt("SMBIOSMemoryType");
                data.MemoryModules.Add(new MemoryModuleInfo
                {
                    Slot = Clean(row.GetString("DeviceLocator")),
                    BankLabel = Clean(row.GetString("BankLabel")),
                    CapacityBytes = row.GetLong("Capacity"),
                    SpeedMhz = row.GetInt("Speed"),
                    ConfiguredSpeedMhz = row.GetInt("ConfiguredClockSpeed"),
                    Manufacturer = Clean(row.GetString("Manufacturer")),
                    PartNumber = Clean(row.GetString("PartNumber")),
                    SmbiosMemoryType = smbiosType,
                    MemoryType = MapMemoryType(smbiosType),
                    FormFactor = MapFormFactor(row.GetInt("FormFactor"))
                });
            }

            obtained = true;
            var moduleSum = data.MemoryModules.Sum(module => module.CapacityBytes ?? 0);
            data.TotalMemoryBytes ??= moduleSum > 0 ? moduleSum : null;
            checks.Add(CheckRecord.Verified("hardware.memory", "Memoria RAM",
                $"{Formatting.Bytes(data.TotalMemoryBytes)} · {data.MemoryModules.Count} módulo(s)",
                "WMI Win32_PhysicalMemory"));
        }
        else
        {
            checks.Add(memoryError is null
                ? CheckRecord.NotVerifiable("hardware.memory", "Memoria RAM",
                    "Windows no devolvió datos de Win32_PhysicalMemory.", "WMI Win32_PhysicalMemory")
                : CollectorErrors.FromWmi("hardware.memory", "Memoria RAM", memoryError));
        }

        var (arrayRows, arrayError) = TryQuery("Win32_PhysicalMemoryArray", TimeSpan.FromSeconds(6));
        if (arrayRows.Count > 0)
        {
            var row = arrayRows[0];
            data.MemorySlotCount = row.GetInt("MemoryDevices");
            var maxKb = row.GetLong("MaxCapacity");
            if (maxKb is > 0)
            {
                data.MemoryMaxCapacityBytes = maxKb * 1024;
            }

            checks.Add(CheckRecord.Verified("hardware.slots", "Ranuras de memoria",
                $"{data.MemorySlotCount?.ToString() ?? "—"} ranuras · máximo " +
                Formatting.Bytes(data.MemoryMaxCapacityBytes),
                "WMI Win32_PhysicalMemoryArray"));
        }
        else if (arrayError is not null)
        {
            checks.Add(CollectorErrors.FromWmi("hardware.slots", "Ranuras de memoria", arrayError));
        }

        var (videoRows, videoError) = TryQuery("Win32_VideoController", TimeSpan.FromSeconds(8));
        if (videoRows.Count > 0)
        {
            foreach (var row in videoRows)
            {
                var horizontal = row.GetInt("CurrentHorizontalResolution");
                var vertical = row.GetInt("CurrentVerticalResolution");
                data.Graphics.Add(new GraphicsAdapterInfo
                {
                    Name = Clean(row.GetString("Name")),
                    AdapterRamBytes = row.GetLong("AdapterRAM"),
                    DriverVersion = Clean(row.GetString("DriverVersion")),
                    DriverDate = row.GetDateTime("DriverDate"),
                    VideoProcessor = Clean(row.GetString("VideoProcessor")),
                    CurrentResolution = horizontal is > 0 && vertical is > 0
                        ? $"{horizontal} x {vertical}"
                        : null,
                    Status = Clean(row.GetString("Status"))
                });
            }

            checks.Add(CheckRecord.Verified("hardware.gpu", "Adaptadores de video",
                string.Join(" | ", data.Graphics.Select(gpu => gpu.Name).Where(n => n is not null)),
                "WMI Win32_VideoController"));
        }
        else
        {
            checks.Add(videoError is null
                ? CheckRecord.NotDetected("hardware.gpu", "Adaptadores de video",
                    "No se detectaron adaptadores de video.", "WMI Win32_VideoController")
                : CollectorErrors.FromWmi("hardware.gpu", "Adaptadores de video", videoError));
        }

        var (batteryRows, batteryError) = TryQuery("Win32_Battery", TimeSpan.FromSeconds(6));
        if (batteryRows.Count > 0)
        {
            foreach (var row in batteryRows)
            {
                data.Batteries.Add(new BatteryInfo
                {
                    Name = Clean(row.GetString("Name")),
                    EstimatedChargePercent = row.GetInt("EstimatedChargeRemaining"),
                    Status = MapBatteryStatus(row.GetInt("BatteryStatus"))
                });
            }

            checks.Add(CheckRecord.Verified("hardware.battery", "Batería",
                $"{data.Batteries.Count} batería(s) detectada(s)", "WMI Win32_Battery"));
        }
        else if (batteryError is null)
        {
            checks.Add(CheckRecord.NotApplicable("hardware.battery", "Batería",
                "El equipo no informa batería (habitual en equipos de escritorio).", "WMI Win32_Battery"));
        }
        else
        {
            checks.Add(CollectorErrors.FromWmi("hardware.battery", "Batería", batteryError));
        }

        var result = new CategoryResult<HardwareData>
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
            Apply = snapshot => snapshot.Hardware = result
        });
    }

    public void ApplyUnavailable(InventorySnapshot target, CheckStatus status, string message, string? errorType)
    {
        target.Hardware = CategoryResult<HardwareData>.Unavailable(Id, Title, status, message, errorType);
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

    private static string? MapMemoryType(int? code) => code switch
    {
        20 => "DDR",
        21 => "DDR2",
        22 => "DDR2 FB-DIMM",
        24 => "DDR3",
        26 => "DDR4",
        27 => "LPDDR",
        28 => "LPDDR2",
        29 => "LPDDR3",
        30 => "LPDDR4",
        34 => "DDR5",
        35 => "LPDDR4X",
        36 => "LPDDR5",
        37 => "LPDDR5X",
        _ => null
    };

    private static string? MapFormFactor(int? code) => code switch
    {
        8 => "DIMM",
        12 => "SODIMM",
        13 => "SRIMM",
        15 => "FB-DIMM",
        _ => null
    };

    private static string? MapBatteryStatus(int? status) => status switch
    {
        1 => "Descargando",
        2 => "Conectado a corriente alterna",
        3 => "Carga completa",
        4 => "Batería baja",
        5 => "Batería crítica",
        6 => "Cargando",
        7 => "Cargando (alta)",
        8 => "Cargando (baja)",
        9 => "Cargando (crítica)",
        10 => "Indefinido",
        11 => "Parcialmente cargada",
        _ => null
    };

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
