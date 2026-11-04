using System.IO;
using System.Security.Principal;
using AuditaPC.Core.Inventory;
using AuditaPC.Core.Reporting;
using AuditaPC.Core.Storage;

namespace AuditaPC.App.Services;

/// <summary>Servicios compartidos por la aplicación. No contiene estado de red.</summary>
public sealed class AppServices
{
    private readonly AppPaths _paths;

    public AppServices(AppPaths paths, AppSettings settings)
    {
        _paths = paths;
        Settings = settings;
        // AppPathResolver ya resolvió rutas relativas respecto al ejecutable.
        // No volver a resolverlas desde el directorio de trabajo actual.
        AuditsRoot = paths.AuditsRoot;
        Repository = new AuditRepository();
        Photos = new PhotoStore();
        Reports = new ReportWriter();
        Runner = new InventoryRunner(maxConcurrency: 3);
        IsElevated = DetectElevation();
    }

    public AppSettings Settings { get; }
    public string AuditsRoot { get; private set; }
    public string SettingsFile => _paths.SettingsFile;
    public string ExecutableDirectory => _paths.ExecutableDirectory;
    public IReadOnlyList<string> StartupWarnings => _paths.Warnings;
    public bool SettingsArePortable => _paths.SettingsArePortable;
    public bool IsElevated { get; }
    public AuditRepository Repository { get; }
    public PhotoStore Photos { get; }
    public ReportWriter Reports { get; }
    public InventoryRunner Runner { get; }

    public bool IsPortable =>
        AppPathResolver.IsUnder(AuditsRoot, ExecutableDirectory);

    public string Technician => Settings.TechnicianName ?? string.Empty;

    public void SetAuditsRoot(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Selecciona una carpeta válida.", nameof(path));
        }

        var full = Path.GetFullPath(path);
        Directory.CreateDirectory(full);
        if (!AppPathResolver.CanWrite(full))
        {
            throw new UnauthorizedAccessException(
                "No se puede escribir en la carpeta seleccionada. Elige una ubicación con permisos de escritura.");
        }

        AuditsRoot = full;
        Settings.AuditsRoot = AppPathResolver.IsUnder(full, ExecutableDirectory)
            ? Path.GetRelativePath(ExecutableDirectory, full)
            : full;
        SaveSettings();
    }

    public void SetTechnician(string? technician)
    {
        Settings.TechnicianName = string.IsNullOrWhiteSpace(technician) ? null : technician.Trim();
        SaveSettings();
    }

    public void SetOnlineLicenseChecksEnabled(bool enabled)
    {
        Settings.EnableOnlineLicenseChecks = enabled;
        SaveSettings();
    }

    public void SaveSettings() =>
        AppPathResolver.SaveSettings(SettingsFile, Settings);

    private static bool DetectElevation()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

}
