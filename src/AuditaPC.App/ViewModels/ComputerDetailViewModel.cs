using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using AuditaPC.App.Services;
using AuditaPC.App.Views;
using AuditaPC.Core;
using AuditaPC.Core.Model;
using AuditaPC.Core.Storage;
using Microsoft.Win32;

namespace AuditaPC.App.ViewModels;

public sealed class PhotoRow
{
    public PhotoRow(PhotoAttachment attachment, BitmapImage? image)
    {
        Attachment = attachment;
        Image = image;
    }

    public PhotoAttachment Attachment { get; }
    public BitmapImage? Image { get; }
    public string Description => string.IsNullOrWhiteSpace(Attachment.Description)
        ? "Sin descripción"
        : Attachment.Description!;
    public string FileName => Attachment.OriginalFileName;
    public string Meta => $"{Formatting.Date(Attachment.AddedAt)} · {Formatting.Bytes(Attachment.SizeBytes)}";
    public bool HasImage => Image is not null;
}

public sealed class CaptureRow
{
    public CaptureRow(CaptureReference reference, bool isCurrent)
    {
        Reference = reference;
        IsCurrent = isCurrent;
    }

    public CaptureReference Reference { get; }
    public bool IsCurrent { get; }
    public string DateText => Formatting.DateTimeLong(Reference.CapturedAt);
    public string Id => Reference.Id;
    public string File => Reference.File;
    public string ShortHash => string.IsNullOrWhiteSpace(Reference.Sha256)
        ? "—"
        : Reference.Sha256.Length <= 16 ? Reference.Sha256 : Reference.Sha256[..16] + "…";
    public string AppVersion => Reference.AppVersion;
    public string CurrentText => IsCurrent ? "Vigente" : string.Empty;
}

public sealed class ComputerDetailViewModel : ObservableObject
{
    private readonly AppServices _services;
    private readonly AuditWorkspace _workspace;
    private readonly ComputerListItem _item;

    private CaptureRecord? _currentCapture;
    private string _manualDisplayName = string.Empty;
    private string _manualAssetCode = string.Empty;
    private string _manualResponsible = string.Empty;
    private string _manualLocation = string.Empty;
    private string _manualNotes = string.Empty;
    private string _manualIssues = string.Empty;
    private string _manualInterventions = string.Empty;
    private string _manualPending = string.Empty;
    private string _manualRecommendations = string.Empty;
    private string _saveStatus = string.Empty;
    private bool _isDirty;

    public ComputerDetailViewModel(AppServices services, AuditWorkspace workspace, ComputerListItem item)
    {
        _services = services;
        _workspace = workspace;
        _item = item;
        Record = item.Record;

        SaveManualCommand = new RelayCommand(SaveManual);
        AddPhotoCommand = new RelayCommand(AddPhoto);
        DeletePhotoCommand = new RelayCommand(DeletePhoto);
        SetCurrentCaptureCommand = new RelayCommand(SetCurrentCapture);
        OpenPhotoCommand = new RelayCommand(OpenPhoto);
        OpenCaptureFolderCommand = new RelayCommand(OpenCaptureFolder);
        OpenProgramsCommand = new RelayCommand(OpenPrograms);

        LoadManualFields();
        Reload();
    }

    public ComputerRecord Record { get; }
    public ObservableCollection<CategoryViewModel> Categories { get; } = new();
    public ObservableCollection<CheckRow> Checks { get; } = new();
    public ObservableCollection<CheckRow> ReviewChecks { get; } = new();
    public ObservableCollection<PhotoRow> Photos { get; } = new();
    public ObservableCollection<CaptureRow> History { get; } = new();

    public RelayCommand SaveManualCommand { get; }
    public RelayCommand AddPhotoCommand { get; }
    public RelayCommand DeletePhotoCommand { get; }
    public RelayCommand SetCurrentCaptureCommand { get; }
    public RelayCommand OpenPhotoCommand { get; }
    public RelayCommand OpenCaptureFolderCommand { get; }
    public RelayCommand OpenProgramsCommand { get; }

    public string DisplayName => string.IsNullOrWhiteSpace(Record.DisplayName)
        ? "Equipo sin nombre"
        : Record.DisplayName;

    public string ManufacturerModel => _item.ManufacturerModel;
    public string Serial => _item.Serial;
    public string OperatingSystem => _item.OperatingSystem;
    public string AssetCode => _item.AssetCode;
    public string Location => _item.Location;
    public string CapturesText => _item.CapturesText;
    public string LastCaptureText => _item.LastCaptureText;
    public bool HasCapture => _currentCapture is not null;
    public bool HasPhotos => Photos.Count > 0;
    public bool HasHistory => History.Count > 0;
    public bool HasReviewChecks => ReviewChecks.Count > 0;

    public string CaptureDateText => _currentCapture is null
        ? "Sin captura automática"
        : $"Captura del {Formatting.DateTimeLong(_currentCapture.CapturedAt)}";

    public int ProblemCount => Checks.Count(check => check.RequiresReview);
    public int IncompleteCount => Checks.Count(check => check.Status.IsIncomplete());
    public CheckStatus WorstStatus => _item.WorstStatus;
    public string StatusText => WorstStatus.ToDisplayText();

    public string ManualDisplayName
    {
        get => _manualDisplayName;
        set { if (Set(ref _manualDisplayName, value)) MarkDirty(); }
    }

    public string ManualAssetCode
    {
        get => _manualAssetCode;
        set { if (Set(ref _manualAssetCode, value)) MarkDirty(); }
    }

    public string ManualResponsible
    {
        get => _manualResponsible;
        set { if (Set(ref _manualResponsible, value)) MarkDirty(); }
    }

    public string ManualLocation
    {
        get => _manualLocation;
        set { if (Set(ref _manualLocation, value)) MarkDirty(); }
    }

    public string ManualNotes
    {
        get => _manualNotes;
        set { if (Set(ref _manualNotes, value)) MarkDirty(); }
    }

    public string ManualIssues
    {
        get => _manualIssues;
        set { if (Set(ref _manualIssues, value)) MarkDirty(); }
    }

    public string ManualInterventions
    {
        get => _manualInterventions;
        set { if (Set(ref _manualInterventions, value)) MarkDirty(); }
    }

    public string ManualPending
    {
        get => _manualPending;
        set { if (Set(ref _manualPending, value)) MarkDirty(); }
    }

    public string ManualRecommendations
    {
        get => _manualRecommendations;
        set { if (Set(ref _manualRecommendations, value)) MarkDirty(); }
    }

    public string SaveStatus
    {
        get => _saveStatus;
        private set => Set(ref _saveStatus, value);
    }

    public bool IsDirty
    {
        get => _isDirty;
        private set => Set(ref _isDirty, value);
    }

    public void OpenPrograms(object? parameter)
    {
        if (parameter is not CategoryViewModel category || !category.HasPrograms)
        {
            return;
        }

        var window = new InstalledProgramsWindow
        {
            Owner = Application.Current.MainWindow,
            DataContext = new InstalledProgramsViewModel(category.Programs)
        };
        window.ShowDialog();
    }

    public void Reload()
    {
        _currentCapture = null;
        var reference = Record.CurrentCapture;
        if (reference is not null)
        {
            try
            {
                _currentCapture = _workspace.LoadCapture(reference);
            }
            catch (Exception ex)
            {
                SaveStatus = "No se pudo leer la captura vigente: " + ex.Message;
            }
        }

        Categories.Clear();
        foreach (var category in InventoryViewModelBuilder.Build(_currentCapture?.Inventory))
        {
            Categories.Add(category);
        }

        Checks.Clear();
        foreach (var check in InventoryViewModelBuilder.BuildChecks(_currentCapture?.Inventory))
        {
            Checks.Add(check);
        }

        ReviewChecks.Clear();
        foreach (var check in Checks.Where(check =>
                     check.RequiresReview
                     || check.Status.IsIncomplete()
                     || check.Status is CheckStatus.NotTestedOffline or CheckStatus.KnownLimitation))
        {
            ReviewChecks.Add(check);
        }

        Photos.Clear();
        foreach (var photo in Record.Photos.OrderByDescending(photo => photo.AddedAt))
        {
            Photos.Add(new PhotoRow(photo, LoadImage(photo.DisplayPath)));
        }

        History.Clear();
        foreach (var capture in Record.Captures.OrderByDescending(capture => capture.CapturedAt))
        {
            History.Add(new CaptureRow(capture, capture.Id == Record.CurrentCapture?.Id));
        }

        LoadManualFields();
        RaiseAll();
    }

    public void AddPhoto()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Seleccionar fotografía",
            Filter = "Imágenes|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.tif;*.tiff|Todos los archivos|*.*",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var description = PhotoDescriptionDialog.Ask(
            Application.Current.MainWindow,
            "Descripción de la fotografía",
            "Describe qué muestra la imagen (opcional).");

        try
        {
            var photo = _services.Photos.AddPhoto(
                _workspace.FolderPath,
                Record.Id,
                dialog.FileName,
                description);
            Record.Photos.Add(photo);
            _workspace.SaveComputer(Record);
            SaveStatus = "Fotografía agregada.";
            Reload();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                Application.Current.MainWindow,
                ex.Message,
                "No se pudo agregar la fotografía",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void DeletePhoto(object? parameter)
    {
        if (parameter is not PhotoRow row)
        {
            return;
        }

        var confirm = MessageBox.Show(
            Application.Current.MainWindow,
            $"¿Eliminar la fotografía «{row.FileName}» de esta ficha?\n\n" +
            "La captura de inventario no se modifica, pero la imagen se borrará de la carpeta de la auditoría.",
            "Eliminar fotografía",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            MessageBoxResult.No);

        if (confirm != MessageBoxResult.Yes)
        {
            return;
        }

        _services.Photos.DeletePhotoFiles(_workspace.FolderPath, row.Attachment);
        Record.Photos.Remove(row.Attachment);
        _workspace.SaveComputer(Record);
        SaveStatus = "Fotografía eliminada.";
        Reload();
    }

    private void OpenPhoto(object? parameter)
    {
        if (parameter is not PhotoRow row)
        {
            return;
        }

        var relative = string.IsNullOrWhiteSpace(row.Attachment.DisplayPath)
            ? row.Attachment.StoredPath
            : row.Attachment.DisplayPath;
        try
        {
            var path = PathGuard.ResolveInside(_workspace.FolderPath, relative);
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                Application.Current.MainWindow,
                ex.Message,
                "No se pudo abrir la fotografía",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void SetCurrentCapture(object? parameter)
    {
        if (parameter is not CaptureRow row)
        {
            return;
        }

        Record.CurrentCaptureId = row.Reference.Id;
        _workspace.SaveComputer(Record);
        SaveStatus = "Captura vigente actualizada.";
        Reload();
    }

    private void OpenCaptureFolder()
    {
        try
        {
            Process.Start(new ProcessStartInfo(_workspace.FolderPath) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                Application.Current.MainWindow,
                ex.Message,
                "No se pudo abrir la carpeta",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    public void SaveManual()
    {
        Record.DisplayName = string.IsNullOrWhiteSpace(ManualDisplayName)
            ? Record.DisplayName
            : ManualDisplayName.Trim();
        Record.Manual.Technician = string.IsNullOrWhiteSpace(_services.Technician)
            ? Record.Manual.Technician
            : _services.Technician;
        Record.Manual.AssetCode = Clean(ManualAssetCode);
        Record.Manual.Responsible = Clean(ManualResponsible);
        Record.Manual.Location = Clean(ManualLocation);
        Record.Manual.Notes = Clean(ManualNotes);
        Record.Manual.IssuesFound = Clean(ManualIssues);
        Record.Manual.Interventions = Clean(ManualInterventions);
        Record.Manual.PendingWork = Clean(ManualPending);
        Record.Manual.Recommendations = Clean(ManualRecommendations);
        Record.Manual.UpdatedAt = DateTimeOffset.Now;

        if (!string.IsNullOrWhiteSpace(Record.Manual.AssetCode))
        {
            Record.Fingerprint.AssetCode = Record.Manual.AssetCode;
        }

        _workspace.SaveComputer(Record);
        _item.Update(Record, _currentCapture);
        IsDirty = false;
        SaveStatus = $"Guardado a las {DateTime.Now:HH:mm}.";
        RaiseAll();
    }

    private void LoadManualFields()
    {
        _manualDisplayName = Record.DisplayName;
        _manualAssetCode = Record.Manual.AssetCode ?? string.Empty;
        _manualResponsible = Record.Manual.Responsible ?? string.Empty;
        _manualLocation = Record.Manual.Location ?? string.Empty;
        _manualNotes = Record.Manual.Notes ?? string.Empty;
        _manualIssues = Record.Manual.IssuesFound ?? string.Empty;
        _manualInterventions = Record.Manual.Interventions ?? string.Empty;
        _manualPending = Record.Manual.PendingWork ?? string.Empty;
        _manualRecommendations = Record.Manual.Recommendations ?? string.Empty;
        RaisePropertyChanged(nameof(ManualDisplayName));
        RaisePropertyChanged(nameof(ManualAssetCode));
        RaisePropertyChanged(nameof(ManualResponsible));
        RaisePropertyChanged(nameof(ManualLocation));
        RaisePropertyChanged(nameof(ManualNotes));
        RaisePropertyChanged(nameof(ManualIssues));
        RaisePropertyChanged(nameof(ManualInterventions));
        RaisePropertyChanged(nameof(ManualPending));
        RaisePropertyChanged(nameof(ManualRecommendations));
    }

    private void MarkDirty()
    {
        IsDirty = true;
        SaveStatus = "Cambios sin guardar.";
    }

    private void RaiseAll()
    {
        RaisePropertyChanged(nameof(DisplayName));
        RaisePropertyChanged(nameof(ManufacturerModel));
        RaisePropertyChanged(nameof(Serial));
        RaisePropertyChanged(nameof(OperatingSystem));
        RaisePropertyChanged(nameof(AssetCode));
        RaisePropertyChanged(nameof(Location));
        RaisePropertyChanged(nameof(CapturesText));
        RaisePropertyChanged(nameof(LastCaptureText));
        RaisePropertyChanged(nameof(HasCapture));
        RaisePropertyChanged(nameof(HasPhotos));
        RaisePropertyChanged(nameof(HasHistory));
        RaisePropertyChanged(nameof(HasReviewChecks));
        RaisePropertyChanged(nameof(CaptureDateText));
        RaisePropertyChanged(nameof(ProblemCount));
        RaisePropertyChanged(nameof(IncompleteCount));
        RaisePropertyChanged(nameof(WorstStatus));
        RaisePropertyChanged(nameof(StatusText));
    }

    private BitmapImage? LoadImage(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return null;
        }

        try
        {
            var path = PathGuard.ResolveInside(_workspace.FolderPath, relativePath);
            if (!File.Exists(path))
            {
                return null;
            }

            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.DecodePixelWidth = 420;
            image.UriSource = new Uri(path);
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch
        {
            return null;
        }
    }

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
