using AuditaPC.Core.Model;

namespace AuditaPC.Core.Inventory.Collectors;

public sealed class NetworkCollector : IInventoryCollector
{
    public string Id => "network";
    public string Title => "Red local";
    public TimeSpan Timeout => TimeSpan.FromSeconds(14);

    public Task<CollectorOutcome> CollectAsync(CollectorContext context)
    {
        var checks = new List<CheckRecord>();
        var data = new NetworkData();
        var obtained = false;

        var (configRows, configError) = TryQuery("Win32_NetworkAdapterConfiguration",
            TimeSpan.FromSeconds(9), "IPEnabled = TRUE");
        var (adapterRows, adapterError) = TryQuery("Win32_NetworkAdapter",
            TimeSpan.FromSeconds(9));

        if (configRows.Count > 0 || adapterRows.Count > 0)
        {
            obtained = true;
        }

        var adapters = adapterRows
            .Where(row => !string.IsNullOrWhiteSpace(row.GetString("DeviceID")))
            .ToDictionary(
                row => row.GetString("DeviceID")!.Trim(),
                row => row,
                StringComparer.OrdinalIgnoreCase);
        var configurations = configRows
            .Where(row => !string.IsNullOrWhiteSpace(row.GetString("Index")))
            .ToDictionary(
                row => row.GetString("Index")!.Trim(),
                row => row,
                StringComparer.OrdinalIgnoreCase);

        var allIndexes = adapters.Keys
            .Concat(configurations.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(index => int.TryParse(index, out var parsed) ? parsed : int.MaxValue)
            .ThenBy(index => index, StringComparer.OrdinalIgnoreCase);

        foreach (var index in allIndexes)
        {
            adapters.TryGetValue(index, out var adapter);
            configurations.TryGetValue(index, out var configuration);

            var description = Clean(configuration?.GetString("Description"))
                              ?? Clean(adapter?.GetString("Name"))
                              ?? Clean(adapter?.GetString("NetConnectionID"))
                              ?? $"Adaptador {index}";

            var mac = Clean(adapter?.GetString("MACAddress"))
                      ?? Clean(configuration?.GetString("MACAddress"));
            var physical = adapter?.GetBool("PhysicalAdapter");
            var virtualAdapter = DetermineVirtual(description, physical);
            var ipv4 = configuration?.GetStringList("IPAddress")
                .Where(IsIpv4)
                .ToList() ?? new List<string>();
            var ipv6 = configuration?.GetStringList("IPAddress")
                .Where(address => !IsIpv4(address))
                .ToList() ?? new List<string>();

            data.Adapters.Add(new NetworkAdapterInfo
            {
                Name = Clean(adapter?.GetString("Name")) ?? description,
                Description = description,
                Manufacturer = Clean(adapter?.GetString("Manufacturer")),
                ConnectionName = Clean(adapter?.GetString("NetConnectionID")),
                MacAddress = mac,
                AdapterType = Clean(adapter?.GetString("AdapterType")),
                IsPhysical = physical,
                IsVirtual = virtualAdapter,
                Status = MapConnectionStatus(adapter?.GetInt("NetConnectionStatus")),
                LinkSpeedBps = adapter?.GetLong("Speed"),
                IPv4Addresses = ipv4,
                IPv6Addresses = ipv6,
                DhcpEnabled = configuration?.GetBool("DHCPEnabled"),
                DhcpServer = Clean(configuration?.GetString("DHCPServer")),
                Gateways = configuration?.GetStringList("DefaultIPGateway") ?? new List<string>(),
                DnsServers = configuration?.GetStringList("DNSServerSearchOrder") ?? new List<string>(),
                DnsSuffix = Clean(configuration?.GetString("DNSDomain"))
                            ?? configuration?.GetStringList("DNSDomainSuffixSearchOrder").FirstOrDefault(),
                InterfaceIndex = configuration?.GetInt("InterfaceIndex") ?? adapter?.GetInt("InterfaceIndex")
            });
        }

        data.PhysicalAdapterCount = data.Adapters.Count(adapter => adapter.IsPhysical == true);

        if (configError is not null)
        {
            checks.Add(CollectorErrors.FromWmi(
                "network.config", "Configuración de red", configError));
        }

        if (adapterError is not null)
        {
            checks.Add(CollectorErrors.FromWmi(
                "network.adapters", "Adaptadores de red", adapterError));
        }

        if (data.Adapters.Count > 0)
        {
            checks.Add(CheckRecord.Verified("network.interfaces", "Interfaces de red",
                $"{data.Adapters.Count} interfaz(es) · {data.PhysicalAdapterCount} física(s)",
                "WMI Win32_NetworkAdapter"));
        }
        else
        {
            checks.Add(CheckRecord.NotDetected("network.interfaces", "Interfaces de red",
                "No se detectaron interfaces de red.", "WMI Win32_NetworkAdapter"));
        }

        var withIp = data.Adapters.Count(adapter => adapter.IPv4Addresses.Count > 0 || adapter.IPv6Addresses.Count > 0);
        checks.Add(withIp > 0
            ? CheckRecord.Verified("network.ip", "Direcciones IP",
                $"{withIp} interfaz(es) con dirección IP activa", "WMI Win32_NetworkAdapterConfiguration")
            : CheckRecord.Detected("network.ip", "Direcciones IP",
                "No se encontraron direcciones IP activas.", "WMI Win32_NetworkAdapterConfiguration"));

        var dhcpAdapters = data.Adapters.Count(adapter => adapter.DhcpEnabled == true);
        if (data.Adapters.Any(adapter => adapter.DhcpEnabled is not null))
        {
            checks.Add(CheckRecord.Verified("network.dhcp", "DHCP",
                $"{dhcpAdapters} interfaz(es) con DHCP habilitado",
                "WMI Win32_NetworkAdapterConfiguration"));
        }
        else
        {
            checks.Add(CheckRecord.NotVerifiable("network.dhcp", "DHCP",
                "Windows no informó el estado DHCP de las interfaces.",
                "WMI Win32_NetworkAdapterConfiguration"));
        }

        checks.Add(CheckRecord.NotApplicable(
            "network.scan",
            "Descubrimiento de otros equipos",
            "AuditaPC no descubre ni escanea otros equipos de la red. Solo consulta este equipo.",
            "Alcance de AuditaPC"));

        var result = new CategoryResult<NetworkData>
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
            Apply = snapshot => snapshot.Network = result
        });
    }

    public void ApplyUnavailable(InventorySnapshot target, CheckStatus status, string message, string? errorType)
    {
        target.Network = CategoryResult<NetworkData>.Unavailable(Id, Title, status, message, errorType);
    }

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

    private static bool IsIpv4(string address)
    {
        return System.Net.IPAddress.TryParse(address, out var parsed)
               && parsed.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork;
    }

    private static bool? DetermineVirtual(string description, bool? physical)
    {
        if (physical == false)
        {
            return true;
        }

        string[] markers =
        {
            "VIRTUAL", "VMWARE", "HYPER-V", "VIRTUALBOX", "VPN", "TAP-", "TUN",
            "WIREGUARD", "LOOPBACK", "WAN MINIPORT", "BLUETOOTH"
        };

        if (markers.Any(marker => description.Contains(marker, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return physical == true ? false : null;
    }

    private static string? MapConnectionStatus(int? status) => status switch
    {
        0 => "Desconectado",
        1 => "Conectando",
        2 => "Conectado",
        3 => "Desconectando",
        4 => "Hardware no presente",
        5 => "Hardware deshabilitado",
        6 => "Fallo de hardware",
        7 => "Sin medios",
        8 => "Autenticando",
        9 => "Autenticación correcta",
        10 => "Autenticación fallida",
        11 => "Dirección inválida",
        12 => "Credenciales requeridas",
        _ => null
    };

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
