using System.Globalization;
using Microsoft.Win32;

namespace AuditaPC.Core.Inventory.Collectors;

public sealed class RegistryReadException : Exception
{
    public RegistryReadException(string message, bool accessDenied, Exception? inner = null)
        : base(message, inner)
    {
        AccessDenied = accessDenied;
    }

    public bool AccessDenied { get; }
}

public static class RegistryHelper
{
    public static RegistryKey? OpenKey(
        RegistryHive hive,
        string subKey,
        RegistryView view = RegistryView.Default)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, view);
            return baseKey.OpenSubKey(subKey, writable: false);
        }
        catch (System.Security.SecurityException ex)
        {
            throw new RegistryReadException(
                $"Windows denegó el acceso al Registro: {subKey}.", accessDenied: true, ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new RegistryReadException(
                $"Windows denegó el acceso al Registro: {subKey}.", accessDenied: true, ex);
        }
        catch (IOException ex)
        {
            throw new RegistryReadException(
                $"No se pudo leer el Registro: {subKey}.", accessDenied: false, ex);
        }
    }

    public static string? GetString(RegistryKey? key, string valueName)
    {
        if (key is null)
        {
            return null;
        }

        try
        {
            return key.GetValue(valueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames)?.ToString();
        }
        catch
        {
            return null;
        }
    }

    public static int? GetInt(RegistryKey? key, string valueName)
    {
        var value = key?.GetValue(valueName);
        if (value is null)
        {
            return null;
        }

        try
        {
            return Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }
        catch
        {
            return int.TryParse(value.ToString(), NumberStyles.Integer,
                CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : null;
        }
    }

    public static long? GetLong(RegistryKey? key, string valueName)
    {
        var value = key?.GetValue(valueName);
        if (value is null)
        {
            return null;
        }

        try
        {
            return Convert.ToInt64(value, CultureInfo.InvariantCulture);
        }
        catch
        {
            return long.TryParse(value.ToString(), NumberStyles.Integer,
                CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : null;
        }
    }

    public static DateTimeOffset? GetUnixSeconds(RegistryKey? key, string valueName)
    {
        var seconds = GetLong(key, valueName);
        if (seconds is null or <= 0 or > 253402300799)
        {
            return null;
        }

        try
        {
            return DateTimeOffset.FromUnixTimeSeconds(seconds.Value).ToLocalTime();
        }
        catch
        {
            return null;
        }
    }

    public static DateTimeOffset? GetFileTime(RegistryKey? key, string valueName)
    {
        var value = GetLong(key, valueName);
        if (value is null or <= 0)
        {
            return null;
        }

        try
        {
            return new DateTimeOffset(DateTime.FromFileTimeUtc(value.Value)).ToLocalTime();
        }
        catch
        {
            return null;
        }
    }

    public static List<string> GetSubKeyNames(RegistryKey? key)
    {
        if (key is null)
        {
            return new List<string>();
        }

        try
        {
            return key.GetSubKeyNames().ToList();
        }
        catch
        {
            return new List<string>();
        }
    }

    public static bool Exists(RegistryHive hive, string subKey, RegistryView view = RegistryView.Default)
    {
        try
        {
            using var key = OpenKey(hive, subKey, view);
            return key is not null;
        }
        catch
        {
            return false;
        }
    }
}
