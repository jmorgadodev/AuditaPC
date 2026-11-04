namespace AuditaPC.Core.Model;

public sealed class NetworkData
{
    public List<NetworkAdapterInfo> Adapters { get; set; } = new();
    public int? PhysicalAdapterCount { get; set; }
}

public sealed class NetworkAdapterInfo
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Manufacturer { get; set; }
    public string? ConnectionName { get; set; }
    public string? MacAddress { get; set; }
    public string? AdapterType { get; set; }
    public bool? IsPhysical { get; set; }
    public bool? IsVirtual { get; set; }
    public string? Status { get; set; }
    public long? LinkSpeedBps { get; set; }
    public List<string> IPv4Addresses { get; set; } = new();
    public List<string> IPv6Addresses { get; set; } = new();
    public bool? DhcpEnabled { get; set; }
    public string? DhcpServer { get; set; }
    public List<string> Gateways { get; set; } = new();
    public List<string> DnsServers { get; set; } = new();
    public string? DnsSuffix { get; set; }
    public int? InterfaceIndex { get; set; }
}
