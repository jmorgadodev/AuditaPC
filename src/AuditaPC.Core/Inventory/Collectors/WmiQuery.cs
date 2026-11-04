using System.Globalization;
using System.Management;

namespace AuditaPC.Core.Inventory.Collectors;

public enum WmiFailureKind
{
    NotFound,
    AccessDenied,
    Timeout,
    Other
}

public sealed class WmiQueryException : Exception
{
    public WmiQueryException(WmiFailureKind kind, string message, Exception? inner = null)
        : base(message, inner)
    {
        Kind = kind;
    }

    public WmiFailureKind Kind { get; }
}

/// <summary>Fila WMI ya materializada; los objetos COM se liberan de inmediato.</summary>
public sealed class WmiRow
{
    private readonly Dictionary<string, object?> _values;

    internal WmiRow(Dictionary<string, object?> values)
    {
        _values = values;
    }

    public bool Has(string name) => _values.ContainsKey(name);

    public object? this[string name] =>
        _values.TryGetValue(name, out var value) ? value : null;

    public string? GetString(string name)
    {
        var value = this[name];
        return value switch
        {
            null => null,
            string text => text,
            string[] array => string.Join(", ", array.Where(s => !string.IsNullOrWhiteSpace(s))),
            Array array => string.Join(", ", array.Cast<object?>().Select(Convert.ToString)),
            _ => Convert.ToString(value, CultureInfo.InvariantCulture)
        };
    }

    public int? GetInt(string name)
    {
        var value = this[name];
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
            return int.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture),
                NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : null;
        }
    }

    public long? GetLong(string name)
    {
        var value = this[name];
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
            return long.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture),
                NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : null;
        }
    }

    public bool? GetBool(string name)
    {
        var value = this[name];
        return value switch
        {
            null => null,
            bool boolean => boolean,
            string text when bool.TryParse(text, out var parsed) => parsed,
            _ => null
        };
    }

    public DateTimeOffset? GetDateTime(string name)
    {
        var value = this[name];
        if (value is null)
        {
            return null;
        }

        try
        {
            if (value is DateTime dateTime)
            {
                return new DateTimeOffset(DateTime.SpecifyKind(dateTime, DateTimeKind.Local));
            }

            if (value is string text && !string.IsNullOrWhiteSpace(text))
            {
                var parsed = ManagementDateTimeConverter.ToDateTime(text);
                return new DateTimeOffset(DateTime.SpecifyKind(parsed, DateTimeKind.Local));
            }
        }
        catch
        {
            return null;
        }

        return null;
    }

    public List<string> GetStringList(string name)
    {
        var value = this[name];
        if (value is null)
        {
            return new List<string>();
        }

        if (value is string text)
        {
            return string.IsNullOrWhiteSpace(text) ? new List<string>() : new List<string> { text };
        }

        if (value is Array array)
        {
            return array.Cast<object?>()
                .Select(item => Convert.ToString(item, CultureInfo.InvariantCulture))
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Select(item => item!)
                .ToList();
        }

        return new List<string>();
    }

    public List<int> GetIntList(string name)
    {
        var value = this[name];
        if (value is null)
        {
            return new List<int>();
        }

        if (value is Array array)
        {
            var result = new List<int>();
            foreach (var item in array)
            {
                try
                {
                    result.Add(Convert.ToInt32(item, CultureInfo.InvariantCulture));
                }
                catch
                {
                    // Se omite el elemento no convertible.
                }
            }

            return result;
        }

        return GetInt(name) is { } single ? new List<int> { single } : new List<int>();
    }
}

public static class WmiQuery
{
    private const string LocalMachineScope = @"\\.\";

    public static IReadOnlyList<WmiRow> Query(
        string className,
        string? namespacePath = null,
        string? where = null,
        TimeSpan? timeout = null,
        string? selectColumns = null)
    {
        var scopePath = LocalMachineScope + (namespacePath ?? @"root\cimv2");
        var queryText = $"SELECT {(string.IsNullOrWhiteSpace(selectColumns) ? "*" : selectColumns)} FROM {className}";
        if (!string.IsNullOrWhiteSpace(where))
        {
            queryText += " WHERE " + where;
        }

        var effectiveTimeout = timeout ?? TimeSpan.FromSeconds(8);

        try
        {
            var scope = new ManagementScope(scopePath);
            var options = new System.Management.EnumerationOptions
            {
                Timeout = effectiveTimeout,
                ReturnImmediately = false,
                Rewindable = false
            };

            using var searcher = new ManagementObjectSearcher(scope, new ObjectQuery(queryText), options);
            using var results = searcher.Get();
            var rows = new List<WmiRow>();

            foreach (ManagementBaseObject item in results)
            {
                using (item)
                {
                    var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                    foreach (PropertyData property in item.Properties)
                    {
                        values[property.Name] = NormalizeValue(property.Value);
                    }

                    rows.Add(new WmiRow(values));
                }
            }

            return rows;
        }
        catch (ManagementException ex)
        {
            var kind = ex.ErrorCode switch
            {
                ManagementStatus.AccessDenied => WmiFailureKind.AccessDenied,
                ManagementStatus.InvalidNamespace => WmiFailureKind.NotFound,
                ManagementStatus.InvalidClass => WmiFailureKind.NotFound,
                ManagementStatus.NotFound => WmiFailureKind.NotFound,
                ManagementStatus.Timedout => WmiFailureKind.Timeout,
                ManagementStatus.CallCanceled => WmiFailureKind.Timeout,
                ManagementStatus.OperationCanceled => WmiFailureKind.Timeout,
                ManagementStatus.ProviderFailure => WmiFailureKind.Other,
                _ => WmiFailureKind.Other
            };
            throw new WmiQueryException(kind, BuildMessage(className, ex), ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new WmiQueryException(WmiFailureKind.AccessDenied,
                $"Windows denegó el acceso a {className}. Se requieren permisos adicionales.", ex);
        }
        catch (TimeoutException ex)
        {
            throw new WmiQueryException(WmiFailureKind.Timeout,
                $"La consulta de {className} excedió el tiempo disponible.", ex);
        }
        catch (Exception ex)
        {
            throw new WmiQueryException(WmiFailureKind.Other,
                BuildMessage(className, ex), ex);
        }
    }

    private static string BuildMessage(string className, Exception ex)
    {
        var message = ex.Message.Trim();
        if (message.Length > 300)
        {
            message = message[..300] + "…";
        }

        return $"No se pudo consultar {className}: {message}";
    }

    private static object? NormalizeValue(object? value)
    {
        return value switch
        {
            null => null,
            string or bool or int or long or uint or ulong or short or ushort
                or byte or sbyte or float or double or decimal => value,
            DateTime dateTime => dateTime,
            Array array => array.Cast<object?>().ToArray(),
            _ => value.ToString()
        };
    }
}
