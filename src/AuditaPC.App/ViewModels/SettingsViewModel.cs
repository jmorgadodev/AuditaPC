using System.Diagnostics;
using System.IO;
using AuditaPC.App.Services;
using AuditaPC.Core;
using Microsoft.Win32;

namespace AuditaPC.App.ViewModels;

public sealed class SettingsViewModel : ObservableObject
{
    private readonly AppServices _services;
    private string _technicianName;
    private bool _onlineLicenseChecksEnabled;
    private string _statusMessage = string.Empty;

    public SettingsViewModel(AppServices services)
    {
        _services = services;
        _technicianName = services.Technician;
        _onlineLicenseChecksEnabled = services.Settings.EnableOnlineLicenseChecks;
        SaveCommand = new RelayCommand(Save);
        ChangeFolderCommand = new RelayCommand(ChangeFolder);
        OpenFolderCommand = new RelayCommand(OpenFolder);
        RootChanged = () => { };
    }

    public RelayCommand SaveCommand { get; }
    public RelayCommand ChangeFolderCommand { get; }
    public RelayCommand OpenFolderCommand { get; }

    public Action RootChanged { get; set; }

    public string TechnicianName
    {
        get => _technicianName;
        set => Set(ref _technicianName, value);
    }

    public bool OnlineLicenseChecksEnabled
    {
        get => _onlineLicenseChecksEnabled;
        set => Set(ref _onlineLicenseChecksEnabled, value);
    }

    public string AuditsRoot => _services.AuditsRoot;
    public string SettingsFile => _services.SettingsFile;
    public string ExecutableDirectory => _services.ExecutableDirectory;
    public bool IsPortable => _services.IsPortable;
    public bool IsElevated => _services.IsElevated;
    public string PortableText => IsPortable
        ? "Modo portable"
        : _services.SettingsArePortable
            ? "Carpeta externa: la ruta configurada puede no estar disponible al cambiar de equipo."
            : "Datos en el perfil local: la carpeta del programa no permite escritura.";
    public string ElevationText => IsElevated ? "Sesión elevada" : "Sesión sin elevación";
    public string VersionText => $"AuditaPC {AppInfo.Version} · esquema {AppInfo.SchemaVersion}";
    public IReadOnlyList<string> Warnings => _services.StartupWarnings;
    public bool HasWarnings => Warnings.Count > 0;

    public string StatusMessage
    {
        get => _statusMessage;
        private set => Set(ref _statusMessage, value);
    }

    public void Save()
    {
        try
        {
            _services.SetTechnician(TechnicianName);
            _services.SetOnlineLicenseChecksEnabled(OnlineLicenseChecksEnabled);
            StatusMessage = "Configuración guardada.";
        }
        catch (Exception ex)
        {
            StatusMessage = "No se pudo guardar la configuración: " + ex.Message;
        }
    }

    public void ChangeFolder()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Seleccionar carpeta de auditorías",
            InitialDirectory = _services.AuditsRoot
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            _services.SetAuditsRoot(dialog.FolderName);
            RaisePropertyChanged(nameof(AuditsRoot));
            RaisePropertyChanged(nameof(IsPortable));
            RaisePropertyChanged(nameof(PortableText));
            StatusMessage = "Carpeta de auditorías actualizada.";
            RootChanged();
        }
        catch (Exception ex)
        {
            StatusMessage = "No se pudo cambiar la carpeta: " + ex.Message;
        }
    }

    public void OpenFolder()
    {
        try
        {
            Directory.CreateDirectory(_services.AuditsRoot);
            Process.Start(new ProcessStartInfo(_services.AuditsRoot) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            StatusMessage = "No se pudo abrir la carpeta: " + ex.Message;
        }
    }
}
