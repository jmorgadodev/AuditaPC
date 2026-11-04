namespace AuditaPC.Core.Inventory.Collectors;

public static class LicenseStatusMapper
{
    public static string Map(int? status) => status switch
    {
        0 => "Sin licencia",
        1 => "Licenciado",
        2 => "Período de gracia de instalación",
        3 => "Período de gracia de activación",
        4 => "Período de gracia por licencia no genuina",
        5 => "Notificación (no activado)",
        6 => "Período de gracia extendido",
        _ => "No determinado"
    };

    public static bool? IsLicensed(int? status) => status is null ? null : status == 1;

    public static string? ExtractChannel(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return null;
        }

        var upper = description.ToUpperInvariant();
        if (upper.Contains("VOLUME_KMSCLIENT", StringComparison.Ordinal)
            || upper.Contains("VOLUME:GVLK", StringComparison.Ordinal))
        {
            return "Volumen (KMS)";
        }

        if (upper.Contains("VOLUME_MAK", StringComparison.Ordinal)
            || upper.Contains("VOLUME:MAK", StringComparison.Ordinal))
        {
            return "Volumen (MAK)";
        }

        if (upper.Contains("RETAIL", StringComparison.Ordinal))
        {
            return "Retail";
        }

        if (upper.Contains("OEM_DM", StringComparison.Ordinal)
            || upper.Contains("OEM:", StringComparison.Ordinal)
            || upper.Contains("OEM ", StringComparison.Ordinal))
        {
            return "OEM";
        }

        if (upper.Contains("SUBSCRIPTION", StringComparison.Ordinal))
        {
            return "Suscripción";
        }

        if (upper.Contains("TIMEBASED", StringComparison.Ordinal))
        {
            return "Basada en tiempo";
        }

        if (upper.Contains("ADDON", StringComparison.Ordinal))
        {
            return "Complemento";
        }

        return null;
    }
}
