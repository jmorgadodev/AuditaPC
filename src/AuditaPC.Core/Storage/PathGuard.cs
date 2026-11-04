using System.Globalization;
using System.Text;

namespace AuditaPC.Core.Storage;

/// <summary>
/// Validación de rutas y nombres. Ninguna ruta proveniente de datos del usuario
/// puede salir de la carpeta de la auditoría.
/// </summary>
public static class PathGuard
{
    private static readonly HashSet<string> ReservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    };

    public static string SanitizeFileName(string? value, string fallback = "sin-nombre", int maxLength = 80)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var invalid = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(value.Length);
        foreach (var character in value.Normalize(NormalizationForm.FormC))
        {
            if (character < 32 || invalid.Contains(character))
            {
                builder.Append('_');
            }
            else
            {
                builder.Append(character);
            }
        }

        var result = builder.ToString().Trim().TrimEnd('.');
        if (result.Length == 0)
        {
            return fallback;
        }

        if (ReservedNames.Contains(result))
        {
            result = "_" + result;
        }

        if (result.Length > maxLength)
        {
            result = result[..maxLength].TrimEnd('.', ' ');
        }

        return string.IsNullOrWhiteSpace(result) ? fallback : result;
    }

    /// <summary>Nombre apto para carpeta e informes, sin acentos ni separadores.</summary>
    public static string Slugify(string? value, string fallback = "Empresa", int maxLength = 48)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        var lastWasSeparator = false;

        foreach (var character in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(character);
            if (category == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (character is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                builder.Append(character);
                lastWasSeparator = false;
            }
            else if (!lastWasSeparator)
            {
                builder.Append('-');
                lastWasSeparator = true;
            }
        }

        var result = builder.ToString().Trim('-');
        if (result.Length > maxLength)
        {
            result = result[..maxLength].Trim('-');
        }

        return result.Length == 0 ? fallback : result;
    }

    public static string ResolveInside(string rootDirectory, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            throw new ArgumentException("La ruta relativa está vacía.", nameof(relativePath));
        }

        if (Path.IsPathRooted(relativePath))
        {
            throw new InvalidOperationException("Se esperaba una ruta relativa, no una ruta absoluta.");
        }

        var root = Path.GetFullPath(rootDirectory);
        var combined = Path.GetFullPath(Path.Combine(root, relativePath));
        var rootWithSeparator = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                                + Path.DirectorySeparatorChar;

        if (!combined.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(combined, root, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("La ruta apunta fuera de la carpeta de la auditoría.");
        }

        return combined;
    }

    public static string ToRelativePath(string rootDirectory, string fullPath)
    {
        var root = Path.GetFullPath(rootDirectory);
        var full = Path.GetFullPath(fullPath);
        var relative = Path.GetRelativePath(root, full);
        return relative.Replace('\\', '/');
    }

    public static string NormalizeIdentifier(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);
        foreach (var character in value.Trim().ToUpperInvariant())
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
            }
        }

        return builder.ToString();
    }

    public static bool IsSafeReportFileName(string fileName) =>
        !string.IsNullOrWhiteSpace(fileName)
        && fileName.IndexOfAny(Path.GetInvalidFileNameChars()) < 0
        && !fileName.Contains("..", StringComparison.Ordinal)
        && fileName.EndsWith(".html", StringComparison.OrdinalIgnoreCase);
}
