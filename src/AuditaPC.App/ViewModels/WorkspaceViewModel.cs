using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Data;
using AuditaPC.App.Services;
using AuditaPC.App.Views;
using AuditaPC.Core;
using AuditaPC.Core.Matching;
using AuditaPC.Core.Model;
using AuditaPC.Core.Storage;

namespace AuditaPC.App.ViewModels;

public sealed class WorkspaceViewModel : ObservableObject
{
    private readonly AppServices _services;
    private readonly AuditWorkspace _workspace;

    private string _searchText = string.Empty;
    private ComputerListItem? _selectedComputer;
    private ComputerDetailViewModel? _detail;
    private bool _isBusy;
    private string _busyMessage = string.Empty;

    public WorkspaceViewModel(AppServices services, AuditWorkspace workspace)
    {
        _services = services;
        _workspace = workspace;
        ComputersView = CollectionViewSource.GetDefaultView(Computers);
        ComputersView.Filter = FilterComputer;

        RefreshCommand = new RelayCommand(Refresh);
        CaptureCommand = new AsyncRelayCommand(CaptureAsync, () => !IsBusy);
        AddManualCommand = new AsyncRelayCommand(AddManualAsync, () => !IsBusy);
        RecoverCommand = new AsyncRelayCommand(RecoverAsync, () => !IsBusy);
        GenerateReportsCommand = new AsyncRelayCommand(GenerateReportsAsync, () => !IsBusy && Computers.Count > 0);
        OpenFolderCommand = new RelayCommand(OpenFolder);
        FieldNotesCommand = new RelayCommand(EditFieldNotes);
        CloseRequested = () => { };
        NewAuditRequested = () => { };
        CloseCommand = new RelayCommand(Close);
        NewAuditCommand = new RelayCommand(() => NewAuditRequested(), () => !IsBusy);

        Refresh();
    }

    public AuditWorkspace Workspace => _workspace;
    public ObservableCollection<ComputerListItem> Computers { get; } = new();
    public ICollectionView ComputersView { get; }

    public RelayCommand RefreshCommand { get; }
    public AsyncRelayCommand CaptureCommand { get; }
    public AsyncRelayCommand AddManualCommand { get; }
    public AsyncRelayCommand RecoverCommand { get; }
    public AsyncRelayCommand GenerateReportsCommand { get; }
    public RelayCommand OpenFolderCommand { get; }
    public RelayCommand FieldNotesCommand { get; }
    public RelayCommand CloseCommand { get; }
    public RelayCommand NewAuditCommand { get; }

    public Action CloseRequested { get; set; }
    public Action NewAuditRequested { get; set; }

    public string CompanyName => _workspace.Manifest.CompanyName;
    public string AuditDateText => _workspace.Manifest.StartDate.ToString("dd-MM-yyyy");
    public string TechnicianText => string.IsNullOrWhiteSpace(_workspace.Manifest.Technician)
        ? "Sin técnico asignado"
        : _workspace.Manifest.Technician!;
    public string FolderPath => _workspace.FolderPath;
    public string AuditId => _workspace.Manifest.Id;
    public string AuditIdShort => AuditId[..Math.Min(8, AuditId.Length)].ToUpperInvariant();
    public string FieldNotesButtonText
    {
        get
        {
            var manifest = _workspace.Manifest;
            var count = new[]
            {
                manifest.VisitSummary, manifest.ConnectivityNotes, manifest.ModulesNotes,
                manifest.ConditionNotes, manifest.FindingsNotes, manifest.ActionPlanNotes,
                manifest.LimitationsNotes, manifest.OtherNotes
            }.Count(value => !string.IsNullOrWhiteSpace(value));
            return count == 0 ? "Notas de terreno" : $"Notas de terreno ({count})";
        }
    }
    public string ComputerCountText => Computers.Count == 1
        ? "1 equipo"
        : $"{Computers.Count} equipos";
    public bool IsEmpty => ComputersView.IsEmpty;
    public bool HasComputers => Computers.Count > 0;

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (Set(ref _searchText, value))
            {
                ComputersView.Refresh();
                RaisePropertyChanged(nameof(IsEmpty));
            }
        }
    }

    public ComputerListItem? SelectedComputer
    {
        get => _selectedComputer;
        set
        {
            if (!Set(ref _selectedComputer, value))
            {
                return;
            }

            Detail = value is null
                ? null
                : new ComputerDetailViewModel(_services, _workspace, value);
        }
    }

    public ComputerDetailViewModel? Detail
    {
        get => _detail;
        private set => Set(ref _detail, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (!Set(ref _isBusy, value))
            {
                return;
            }

            CaptureCommand.RaiseCanExecuteChanged();
            AddManualCommand.RaiseCanExecuteChanged();
            RecoverCommand.RaiseCanExecuteChanged();
            GenerateReportsCommand.RaiseCanExecuteChanged();
            NewAuditCommand.RaiseCanExecuteChanged();
        }
    }

    public string BusyMessage
    {
        get => _busyMessage;
        private set => Set(ref _busyMessage, value);
    }

    public void EditFieldNotes()
    {
        var dialog = new FieldNotesDialog(_workspace.Manifest)
        {
            Owner = Application.Current.MainWindow
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            dialog.ApplyTo(_workspace.Manifest);
            _workspace.SaveManifest();
            RaisePropertyChanged(nameof(FieldNotesButtonText));
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                Application.Current.MainWindow,
                "No se pudieron guardar las notas de terreno: " + ex.Message,
                "AuditaPC",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    public void Refresh()
    {
        var selectedId = SelectedComputer?.Id;
        Computers.Clear();

        foreach (var record in _workspace.LoadComputers())
        {
            CaptureRecord? capture = null;
            var reference = record.CurrentCapture;
            if (reference is not null)
            {
                try
                {
                    capture = _workspace.LoadCapture(reference);
                }
                catch
                {
                    // La ficha se muestra igualmente; el detalle informará del problema.
                }
            }

            Computers.Add(new ComputerListItem(record, capture));
        }

        ComputersView.Refresh();
        RaisePropertyChanged(nameof(ComputerCountText));
        RaisePropertyChanged(nameof(IsEmpty));
        RaisePropertyChanged(nameof(HasComputers));
        GenerateReportsCommand.RaiseCanExecuteChanged();

        SelectedComputer = Computers.FirstOrDefault(item =>
                                 item.Id.Equals(selectedId, StringComparison.OrdinalIgnoreCase))
                             ?? Computers.FirstOrDefault();
    }

    public Task CaptureAsync()
    {
        if (IsBusy)
        {
            return Task.CompletedTask;
        }

        var owner = Application.Current.MainWindow;
        IsBusy = true;
        BusyMessage = "Preparando la captura…";
        try
        {
            var progressViewModel = new CaptureProgressViewModel(_services);
            var progressWindow = new CaptureProgressWindow
            {
                Owner = owner,
                DataContext = progressViewModel
            };
            progressWindow.ShowDialog();

            var snapshot = progressViewModel.Snapshot;
            if (snapshot is null)
            {
                return Task.CompletedTask;
            }

            var fingerprint = FingerprintFactory.FromSnapshot(snapshot);
            var match = new DeviceMatchEngine().FindMatches(
                fingerprint,
                Computers.Select(item => item.Record).ToList());

            var dialog = new MatchComputerDialog(
                snapshot,
                match,
                Computers.Select(item => item.Record).ToList(),
                _services.Technician)
            {
                Owner = owner
            };

            if (dialog.ShowDialog() != true)
            {
                return Task.CompletedTask;
            }

            ComputerRecord record;
            if (dialog.UpdateExisting && dialog.SelectedRecord is not null)
            {
                record = dialog.SelectedRecord;
            }
            else
            {
                record = new ComputerRecord
                {
                    DisplayName = string.IsNullOrWhiteSpace(dialog.DisplayName)
                        ? FingerprintFactory.GetDisplayName(snapshot)
                        : dialog.DisplayName.Trim(),
                    Fingerprint = fingerprint,
                    Manual = { Technician = _services.Technician }
                };
                _workspace.SaveComputer(record);
            }

            if (!string.IsNullOrWhiteSpace(dialog.AssetCode))
            {
                record.Manual.AssetCode = dialog.AssetCode.Trim();
                record.Fingerprint.AssetCode = dialog.AssetCode.Trim();
            }

            var confirmed = FingerprintFactory.FromSnapshot(snapshot, dialog.AssetCode);
            _workspace.SaveCapture(record, snapshot, dialog.CaptureNote, confirmed);
            Refresh();
            SelectedComputer = Computers.FirstOrDefault(item => item.Id == record.Id);

            var problemCount = snapshot.AllChecks().Count(check => check.Status == CheckStatus.Problem);
            var incomplete = snapshot.AllChecks().Count(check => check.Status.IsIncomplete());
            MessageBox.Show(
                owner,
                $"La captura se guardó correctamente.\n\n" +
                $"Comprobaciones con problema: {problemCount}\n" +
                $"Comprobaciones incompletas: {incomplete}\n\n" +
                "Puedes revisar el detalle, completar la información manual y generar los informes.",
                "Captura guardada",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                owner,
                ex.Message,
                "No se pudo completar la captura",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
            BusyMessage = string.Empty;
        }

        return Task.CompletedTask;
    }

    private Task AddManualAsync()
    {
        var owner = Application.Current.MainWindow;
        var name = TextPromptDialog.Ask(
            owner,
            "Agregar equipo manual",
            "Nombre del equipo o identificador interno:",
            "Equipo nuevo");
        if (string.IsNullOrWhiteSpace(name))
        {
            return Task.CompletedTask;
        }

        var record = new ComputerRecord
        {
            DisplayName = name.Trim(),
            Manual = { Technician = _services.Technician }
        };

        _workspace.SaveComputer(record);
        Refresh();
        SelectedComputer = Computers.FirstOrDefault(item => item.Id == record.Id);
        return Task.CompletedTask;
    }

    private Task RecoverAsync()
    {
        try
        {
            var report = _workspace.Recover();
            Refresh();
            var lines = new List<string>
            {
                $"Capturas huérfanas encontradas: {report.OrphanCaptures}",
                $"Capturas recuperadas: {report.RecoveredCaptures}",
                $"Fichas creadas: {report.CreatedComputers}"
            };

            if (report.Messages.Count > 0)
            {
                lines.Add(string.Empty);
                lines.AddRange(report.Messages);
            }

            if (report.Errors.Count > 0)
            {
                lines.Add(string.Empty);
                lines.Add("Errores:");
                lines.AddRange(report.Errors);
            }

            MessageBox.Show(
                Application.Current.MainWindow,
                string.Join(Environment.NewLine, lines),
                "Recuperación de capturas",
                MessageBoxButton.OK,
                report.Errors.Count > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                Application.Current.MainWindow,
                ex.Message,
                "No se pudo recuperar",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }

        return Task.CompletedTask;
    }

    private Task GenerateReportsAsync()
    {
        try
        {
            if (Detail?.IsDirty == true)
            {
                var save = MessageBox.Show(
                    Application.Current.MainWindow,
                    "Hay cambios manuales sin guardar en el equipo seleccionado.\n\n" +
                    "Sí: guardar los cambios e incluirlos en los informes.\n" +
                    "No: generar los informes con la última información guardada.\n" +
                    "Cancelar: volver a la auditoría.",
                    "Guardar antes de generar informes",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Question,
                    MessageBoxResult.Yes);

                if (save == MessageBoxResult.Cancel)
                    return Task.CompletedTask;

                if (save == MessageBoxResult.Yes)
                    Detail.SaveManual();
            }

            // La fuente del informe es siempre el expediente persistido que está abierto,
            // no una lista de pantalla que pueda quedar desactualizada.
            var currentRecords = _workspace.LoadComputers().ToList();
            var bundle = _services.Reports.Generate(_workspace, currentRecords);

            var dialog = new ReportResultDialog(bundle)
            {
                Owner = Application.Current.MainWindow
            };
            dialog.ShowDialog();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                Application.Current.MainWindow,
                ex.Message,
                "No se pudieron generar los informes",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }

        return Task.CompletedTask;
    }

    private void OpenFolder()
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

    private void Close()
    {
        if (!TryClose())
        {
            return;
        }

        CloseRequested();
    }

    /// <summary>
    /// Comprueba si se puede cerrar la auditoría y guarda los cambios manuales
    /// pendientes. Devuelve false si el técnico cancela el cierre.
    /// </summary>
    public bool TryClose()
    {
        if (Detail?.IsDirty == true)
        {
            var save = MessageBox.Show(
                Application.Current.MainWindow,
                "Hay cambios manuales sin guardar en la ficha del equipo.\n\n" +
                "¿Deseas guardarlos antes de cerrar la auditoría?",
                "Cambios sin guardar",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question,
                MessageBoxResult.Yes);

            if (save == MessageBoxResult.Cancel)
            {
                return false;
            }

            if (save == MessageBoxResult.Yes)
            {
                Detail.SaveManual();
            }
        }

        return true;
    }

    private bool FilterComputer(object item)
    {
        if (item is not ComputerListItem computer)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(SearchText))
        {
            return true;
        }

        var term = SearchText.Trim();
        return computer.Name.Contains(term, StringComparison.CurrentCultureIgnoreCase)
               || computer.Serial.Contains(term, StringComparison.CurrentCultureIgnoreCase)
               || computer.AssetCode.Contains(term, StringComparison.CurrentCultureIgnoreCase)
               || computer.ManufacturerModel.Contains(term, StringComparison.CurrentCultureIgnoreCase)
               || computer.OperatingSystem.Contains(term, StringComparison.CurrentCultureIgnoreCase);
    }
}
