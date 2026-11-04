using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Data;
using AuditaPC.App.Services;
using AuditaPC.Core.Model;

namespace AuditaPC.App.ViewModels;

public sealed class HomeViewModel : ObservableObject
{
    private readonly AppServices _services;
    private string _searchText = string.Empty;
    private AuditCatalogItem? _selectedAudit;
    private string _statusMessage = string.Empty;
    private bool _isBusy;

    public HomeViewModel(AppServices services)
    {
        _services = services;
        AuditsView = CollectionViewSource.GetDefaultView(Audits);
        AuditsView.Filter = FilterAudit;

        RefreshCommand = new RelayCommand(Refresh);
        OpenSelectedCommand = new RelayCommand(OpenSelected, () => SelectedAudit is not null);
        OpenRootCommand = new RelayCommand(OpenRoot);
        CreateRequested = () => { };
        OpenRequested = _ => { };
        Refresh();
    }

    public ObservableCollection<AuditCatalogItem> Audits { get; } = new();
    public ICollectionView AuditsView { get; }
    public RelayCommand RefreshCommand { get; }
    public RelayCommand OpenSelectedCommand { get; }
    public RelayCommand OpenRootCommand { get; }

    public Action CreateRequested { get; set; }
    public Action<string> OpenRequested { get; set; }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (Set(ref _searchText, value))
            {
                AuditsView.Refresh();
                RaisePropertyChanged(nameof(IsEmpty));
            }
        }
    }

    public AuditCatalogItem? SelectedAudit
    {
        get => _selectedAudit;
        set
        {
            if (Set(ref _selectedAudit, value))
            {
                OpenSelectedCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsEmpty => AuditsView.IsEmpty;
    public bool HasAudits => !IsEmpty;

    public string StatusMessage
    {
        get => _statusMessage;
        private set => Set(ref _statusMessage, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set => Set(ref _isBusy, value);
    }

    public string AuditsRoot => _services.AuditsRoot;

    public void Refresh()
    {
        IsBusy = true;
        try
        {
            var selectedFolder = SelectedAudit?.FolderPath;
            Audits.Clear();
            foreach (var item in _services.Repository.ListAudits(_services.AuditsRoot))
            {
                Audits.Add(item);
            }

            SelectedAudit = Audits.FirstOrDefault(item =>
                                 item.FolderPath.Equals(selectedFolder, StringComparison.OrdinalIgnoreCase))
                             ?? Audits.FirstOrDefault();

            StatusMessage = Audits.Count == 0
                ? "Todavía no hay auditorías en esta carpeta."
                : $"{Audits.Count} auditoría(s) encontrada(s).";
            AuditsView.Refresh();
            RaisePropertyChanged(nameof(IsEmpty));
            RaisePropertyChanged(nameof(HasAudits));
            RaisePropertyChanged(nameof(AuditsRoot));
        }
        catch (Exception ex)
        {
            StatusMessage = "No se pudo leer el catálogo: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void OpenSelected()
    {
        if (SelectedAudit is not null)
        {
            OpenRequested(SelectedAudit.FolderPath);
        }
    }

    private void OpenRoot()
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

    private bool FilterAudit(object item)
    {
        if (item is not AuditCatalogItem audit)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(SearchText))
        {
            return true;
        }

        var term = SearchText.Trim();
        return (audit.Manifest?.CompanyName?.Contains(term, StringComparison.CurrentCultureIgnoreCase) ?? false)
               || (audit.Manifest?.Technician?.Contains(term, StringComparison.CurrentCultureIgnoreCase) ?? false)
               || audit.FolderName.Contains(term, StringComparison.CurrentCultureIgnoreCase);
    }
}
