namespace AuditaPC.Core.Storage;

public sealed class AppSettings
{
    public int SchemaNumber { get; set; } = AppInfo.SchemaNumber;
    public string? AuditsRoot { get; set; }
    public string? TechnicianName { get; set; }
    public string? LastAuditId { get; set; }
    public bool EnableOnlineLicenseChecks { get; set; }
}

/// <summary>
/// Resuelve la ubicación de datos sin depender de una letra de unidad fija.
/// Prefiere la carpeta del ejecutable (pendrive) y usa LocalAppData si no es
/// escribible. Las rutas ubicadas junto al ejecutable se guardan de forma relativa.
/// </summary>
public sealed class AppPaths
{
    public string ExecutableDirectory { get; init; } = string.Empty;
    public string AuditsRoot { get; init; } = string.Empty;
    public string SettingsFile { get; init; } = string.Empty;
    public AppSettings Settings { get; init; } = new();
    public bool IsPortable { get; init; }
    public bool SettingsArePortable { get; init; }
    public List<string> Warnings { get; init; } = new();
}

public static class AppPathResolver
{
    public static AppPaths Resolve(
        string executableDirectory,
        string? localAppData = null,
        AppSettings? settings = null)
    {
        executableDirectory = Path.GetFullPath(executableDirectory);
        localAppData = string.IsNullOrWhiteSpace(localAppData)
            ? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
            : Path.GetFullPath(localAppData);

        if (string.IsNullOrWhiteSpace(localAppData))
        {
            localAppData = Path.GetTempPath();
        }

        var warnings = new List<string>();
        var executableWritable = CanWrite(executableDirectory);
        var settingsDirectory = executableWritable
            ? executableDirectory
            : Path.Combine(localAppData, AppInfo.ProductName);

        Directory.CreateDirectory(settingsDirectory);

        var settingsFile = Path.Combine(settingsDirectory, "AuditaPC.config.json");
        settings ??= LoadSettings(settingsFile, warnings);

        // La versión anterior guardaba las auditorías dentro de Datos\\Auditorias.
        // Migra ese valor predeterminado a una carpeta directa junto al portable.
        var legacyDefault = Path.Combine(AppInfo.DataFolderName, AppInfo.AuditRootFolderName);
        var configuredRootText = settings.AuditsRoot?
            .Trim()
            .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar)
            .TrimEnd(Path.DirectorySeparatorChar);
        var legacyRootText = legacyDefault
            .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar)
            .TrimEnd(Path.DirectorySeparatorChar);
        if (!string.IsNullOrWhiteSpace(configuredRootText)
            && !Path.IsPathRooted(configuredRootText)
            && string.Equals(configuredRootText, legacyRootText, StringComparison.OrdinalIgnoreCase))
        {
            settings.AuditsRoot = AppInfo.AuditRootFolderName;
        }

        var defaultRoot = Path.Combine(executableDirectory, AppInfo.AuditRootFolderName);
        var fallbackRoot = Path.Combine(localAppData, AppInfo.ProductName, AppInfo.AuditRootFolderName);

        var auditsRoot = defaultRoot;
        var portable = executableWritable;

        if (!string.IsNullOrWhiteSpace(settings.AuditsRoot))
        {
            try
            {
                var configuredRoot = ResolveConfiguredRoot(settings.AuditsRoot, executableDirectory);
                auditsRoot = configuredRoot;
                portable = IsUnder(configuredRoot, executableDirectory);

                // Migra configuraciones antiguas con ruta absoluta a una ruta
                // relativa cuando el destino está dentro de la carpeta portable.
                if (portable)
                {
                    settings.AuditsRoot = Path.GetRelativePath(executableDirectory, configuredRoot);
                }
            }
            catch (Exception ex)
            {
                warnings.Add(
                    $"La carpeta configurada para auditorías no es válida ({ex.Message}). " +
                    "Se utilizará la ubicación predeterminada.");
                auditsRoot = defaultRoot;
                portable = executableWritable;
            }
        }

        try
        {
            Directory.CreateDirectory(auditsRoot);
            if (!CanWrite(auditsRoot))
            {
                throw new IOException("La carpeta no permite escritura.");
            }
        }
        catch (Exception ex)
        {
            if (!string.Equals(auditsRoot, fallbackRoot, StringComparison.OrdinalIgnoreCase))
            {
                warnings.Add(
                    $"No se pudo usar {auditsRoot} ({ex.Message}). Se utilizará {fallbackRoot}.");
            }

            auditsRoot = fallbackRoot;
            portable = false;
            Directory.CreateDirectory(auditsRoot);
        }

        if (!executableWritable)
        {
            warnings.Add(
                "AuditaPC no puede escribir junto al ejecutable; la configuración y los datos " +
                "se guardan en el perfil local. Copia la aplicación a una carpeta escribible para " +
                "mantener el modo portable.");
        }

        return new AppPaths
        {
            ExecutableDirectory = executableDirectory,
            AuditsRoot = auditsRoot,
            SettingsFile = settingsFile,
            Settings = settings,
            IsPortable = portable,
            SettingsArePortable = executableWritable,
            Warnings = warnings
        };
    }

    private static string ResolveConfiguredRoot(string configuredRoot, string executableDirectory)
    {
        return Path.IsPathRooted(configuredRoot)
            ? Path.GetFullPath(configuredRoot)
            : Path.GetFullPath(Path.Combine(executableDirectory, configuredRoot));
    }

    public static AppSettings LoadSettings(string path, List<string>? warnings = null)
    {
        try
        {
            if (!File.Exists(path))
            {
                return new AppSettings();
            }

            var json = File.ReadAllText(path);
            return JsonDefaults.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch (Exception ex)
        {
            warnings?.Add(
                $"No se pudo leer la configuración ({ex.Message}). Se usarán valores predeterminados.");
            return new AppSettings();
        }
    }

    public static void SaveSettings(string path, AppSettings settings)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var bytes = JsonDefaults.SerializeToUtf8Bytes(settings);
        new AtomicFileWriter().WriteAtomic(path, bytes);
    }

    public static bool CanWrite(string directory)
    {
        try
        {
            Directory.CreateDirectory(directory);
            var probe = Path.Combine(
                directory, ".auditapc-write-" + Guid.NewGuid().ToString("N") + ".tmp");
            using (var stream = new FileStream(
                       probe, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.WriteByte(0);
                stream.Flush(true);
            }

            File.Delete(probe);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static bool IsUnder(string path, string root)
    {
        var normalizedPath = Path.GetFullPath(path)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var normalizedRoot = Path.GetFullPath(root)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        return normalizedPath.Equals(normalizedRoot, StringComparison.OrdinalIgnoreCase)
               || normalizedPath.StartsWith(
                   normalizedRoot + Path.DirectorySeparatorChar,
                   StringComparison.OrdinalIgnoreCase);
    }
}
