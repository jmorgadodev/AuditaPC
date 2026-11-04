using System.Globalization;

namespace AuditaPC.Core;

public static class Formatting
{
    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("es-CL");

    public static string Bytes(long? bytes)
    {
        if (bytes is null)
        {
            return "—";
        }

        var value = bytes.Value;
        if (value < 0)
        {
            return "—";
        }

        if (value >= 1024L * 1024 * 1024)
        {
            return (value / (1024d * 1024 * 1024)).ToString("#,##0.0", Culture) + " GB";
        }

        if (value >= 1024L * 1024)
        {
            return (value / (1024d * 1024)).ToString("#,##0", Culture) + " MB";
        }

        if (value >= 1024)
        {
            return (value / 1024d).ToString("#,##0", Culture) + " KB";
        }

        return value.ToString("#,##0", Culture) + " B";
    }

    public static string Percent(double? value)
    {
        if (value is null)
        {
            return "—";
        }

        return value.Value.ToString("0.0", Culture) + " %";
    }

    public static string LinkSpeed(long? bitsPerSecond)
    {
        if (bitsPerSecond is null or <= 0)
        {
            return "—";
        }

        var value = bitsPerSecond.Value;
        if (value >= 1_000_000_000)
        {
            return (value / 1_000_000_000d).ToString("0.0", Culture) + " Gbps";
        }

        if (value >= 1_000_000)
        {
            return (value / 1_000_000d).ToString("0", Culture) + " Mbps";
        }

        if (value >= 1_000)
        {
            return (value / 1_000d).ToString("0", Culture) + " Kbps";
        }

        return value.ToString("#,##0", Culture) + " bps";
    }

    public static string Date(DateTimeOffset? value) =>
        value is null ? "—" : value.Value.ToString("dd-MM-yyyy HH:mm", Culture);

    public static string DateOnly(DateOnly? value) =>
        value is null ? "—" : value.Value.ToString("dd-MM-yyyy", Culture);

    public static string DateTimeLong(DateTimeOffset? value) =>
        value is null ? "—" : value.Value.ToString("dd-MM-yyyy HH:mm:ss", Culture);

    public static string Duration(long milliseconds)
    {
        if (milliseconds < 1000)
        {
            return milliseconds + " ms";
        }

        return (milliseconds / 1000d).ToString("0.0", Culture) + " s";
    }

    public static string OrDash(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "—" : value.Trim();

    public static string YesNo(bool? value, string yes = "Sí", string no = "No") =>
        value is null ? "—" : value.Value ? yes : no;

    public static string Plural(int count, string singular, string plural) =>
        count == 1 ? $"{count} {singular}" : $"{count} {plural}";
}
