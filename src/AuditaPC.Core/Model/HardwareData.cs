namespace AuditaPC.Core.Model;

public sealed class HardwareData
{
    public List<CpuInfo> Processors { get; set; } = new();
    public long? TotalMemoryBytes { get; set; }
    public int? MemorySlotCount { get; set; }
    public long? MemoryMaxCapacityBytes { get; set; }
    public List<MemoryModuleInfo> MemoryModules { get; set; } = new();
    public List<GraphicsAdapterInfo> Graphics { get; set; } = new();
    public List<BatteryInfo> Batteries { get; set; } = new();
}

public sealed class CpuInfo
{
    public string? Name { get; set; }
    public string? Manufacturer { get; set; }
    public int? Cores { get; set; }
    public int? LogicalProcessors { get; set; }
    public int? MaxClockMhz { get; set; }
    public string? Socket { get; set; }
    public int? L2CacheKb { get; set; }
    public int? L3CacheKb { get; set; }
    public string? ProcessorId { get; set; }
}

public sealed class MemoryModuleInfo
{
    public string? Slot { get; set; }
    public string? BankLabel { get; set; }
    public long? CapacityBytes { get; set; }
    public int? SpeedMhz { get; set; }
    public int? ConfiguredSpeedMhz { get; set; }
    public string? Manufacturer { get; set; }
    public string? PartNumber { get; set; }
    public int? SmbiosMemoryType { get; set; }
    public string? MemoryType { get; set; }
    public string? FormFactor { get; set; }
}

public sealed class GraphicsAdapterInfo
{
    public string? Name { get; set; }
    public long? AdapterRamBytes { get; set; }
    public string? DriverVersion { get; set; }
    public DateTimeOffset? DriverDate { get; set; }
    public string? VideoProcessor { get; set; }
    public string? CurrentResolution { get; set; }
    public string? Status { get; set; }
}

public sealed class BatteryInfo
{
    public string? Name { get; set; }
    public int? EstimatedChargePercent { get; set; }
    public string? Status { get; set; }
}
