using AuditaPC.Core.Model;

namespace AuditaPC.Core.Matching;

/// <summary>Normalización y detección de identificadores genéricos.</summary>
public static class MatchNormalization
{
    private static readonly HashSet<string> GenericValues = new(StringComparer.Ordinal)
    {
        "SYSTEMSERIALNUMBER", "SYSTEMSERIAL", "BASEPLATESERIALNUMBER", "BASEPLATESERIAL",
        "CHASSISSERIALNUMBER", "CHASSISSERIAL", "DEFAULTSTRING", "TOBEFILLEDBYOEM",
        "TOBEFILLEDBYOEMINC", "FILLEDBYOEM", "OEM", "OEMDEFAULTSTRING", "NONE",
        "NOTSPECIFIED", "NOTAVAILABLE", "NOTAPPLICABLE", "UNKNOWN", "INVALID",
        "SERIALNUMBER", "SERIAL", "NOPARTNUMBER", "NOPART", "NA", "N/A",
        "123456789", "0123456789", "1234567890", "000000000000000",
        "FFFFFFFFFFFFFFFF", "FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF", "0", "1",
        "VMWARE", "VIRTUALBOX", "DEFAULT", "NOTSET"
    };

    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new System.Text.StringBuilder(value.Length);
        foreach (var character in value.Trim().ToUpperInvariant())
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
            }
        }

        return builder.ToString();
    }

    public static bool IsGeneric(string? value)
    {
        var normalized = Normalize(value);
        if (normalized.Length < 3)
        {
            return true;
        }

        if (GenericValues.Contains(normalized))
        {
            return true;
        }

        // Todos ceros o todos F: valores habituales de firmware sin programar.
        var allZero = true;
        var allF = true;
        foreach (var character in normalized)
        {
            if (character != '0')
            {
                allZero = false;
            }

            if (character != 'F')
            {
                allF = false;
            }
        }

        return allZero || allF;
    }

    public static bool IsUsable(string? value) => !string.IsNullOrWhiteSpace(value) && !IsGeneric(value);

    public static bool Equal(string? left, string? right)
    {
        if (!IsUsable(left) || !IsUsable(right))
        {
            return false;
        }

        return string.Equals(Normalize(left), Normalize(right), StringComparison.Ordinal);
    }

    public static bool EqualLoose(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
        {
            return false;
        }

        return string.Equals(Normalize(left), Normalize(right), StringComparison.Ordinal);
    }
}
