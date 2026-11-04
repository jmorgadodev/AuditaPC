using System.Windows;
using AuditaPC.App.Services;
using AuditaPC.App.Views;
using AuditaPC.Core;
using AuditaPC.Core.Storage;

namespace AuditaPC.App.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly AppServices _services;
    private object? _currentPage;
    private WorkspaceViewModel? _workspace;
    private bool _isAuditsSelected = true;
    private bool _isSettingsSelected;
    private bool _isSidebarExpanded = true;

    public MainViewModel(AppServices services)
    {
        _services = services;
        Home = new HomeViewModel(services)
        {
            CreateRequested = CreateAudit,
            OpenRequested = OpenAudit
        };
        Settings = new SettingsViewModel(services)
        {
            RootChanged = () =>
            {
                Home.Refresh();
                RaisePropertyChanged(nameof(StorageModeText));
            }
        };
        ToggleSidebarCommand = new RelayCommand(ToggleSidebar);

        _currentPage = Home;
    }

    public HomeViewModel Home { get; }
    public SettingsViewModel Settings { get; }
    public WorkspaceViewModel? Workspace => _workspace;
    public RelayCommand ToggleSidebarCommand { get; }

    public bool IsSidebarExpanded
    {
        get => _isSidebarExpanded;
        set
        {
            if (!Set(ref _isSidebarExpanded, value))
            {
                return;
            }

            RaisePropertyChanged(nameof(SidebarWidth));
            RaisePropertyChanged(nameof(SidebarToggleText));
            RaisePropertyChanged(nameof(ShowAuditContext));
        }
    }

    public GridLength SidebarWidth => new(IsSidebarExpanded ? 252 : 108);
    public string SidebarToggleText => IsSidebarExpanded ? "‹" : "›";
    public bool ShowAuditContext => IsAuditOpen && IsSidebarExpanded;

    public object? CurrentPage
    {
        get => _currentPage;
        private set => Set(ref _currentPage, value);
    }

    public bool IsAuditsSelected
    {
        get => _isAuditsSelected;
        set
        {
            if (!Set(ref _isAuditsSelected, value) || !value)
            {
                return;
            }

            IsSettingsSelected = false;
            ShowAudits();
        }
    }

    public bool IsSettingsSelected
    {
        get => _isSettingsSelected;
        set
        {
            if (!Set(ref _isSettingsSelected, value) || !value)
            {
                return;
            }

            IsAuditsSelected = false;
            CurrentPage = Settings;
        }
    }

    public bool IsAuditOpen => _workspace is not null;
    public string AuditContext => _workspace?.CompanyName ?? "Sin auditoría abierta";
    public string AuditsRoot => _services.AuditsRoot;
    public bool IsPortable => _services.IsPortable;
    public string StorageModeText => IsPortable
        ? "Modo portable · datos junto al programa"
        : "Datos guardados en el perfil local";

    public void ToggleSidebar() => IsSidebarExpanded = !IsSidebarExpanded;

    public void CreateAudit()
    {
        var dialog = new CreateAuditDialog(_services.Technician)
        {
            Owner = Application.Current.MainWindow
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            if (_workspace is not null && !_workspace.TryClose())
            {
                return;
            }

            var workspace = _services.Repository.CreateAudit(
                _services.AuditsRoot,
                dialog.CompanyName,
                dialog.Technician,
                dialog.Notes,
                DateOnly.FromDateTime(dialog.StartDate));
            OpenWorkspace(workspace);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                Application.Current.MainWindow,
                ex.Message,
                "No se pudo crear la auditoría",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    public void OpenAudit(string folderPath)
    {
        try
        {
            var workspace = _services.Repository.OpenAudit(folderPath);
            OpenWorkspace(workspace);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                Application.Current.MainWindow,
                ex.Message,
                "No se pudo abrir la auditoría",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    public void ShowAudits()
    {
        if (_workspace is not null)
        {
            CurrentPage = _workspace;
            return;
        }

        Home.Refresh();
        CurrentPage = Home;
    }

    public void CloseAudit()
    {
        var workspace = _workspace;
        _workspace = null;
        CurrentPage = Home;
        Home.Refresh();
        workspace?.Workspace.Dispose();
        RaisePropertyChanged(nameof(IsAuditOpen));
        RaisePropertyChanged(nameof(AuditContext));
        RaisePropertyChanged(nameof(ShowAuditContext));
    }

    /// <summary>
    /// Prepara el cierre de la aplicación. Devuelve false si hay cambios sin
    /// guardar y el técnico decide cancelar.
    /// </summary>
    public bool TryCloseForShutdown()
    {
        if (_workspace is not null && !_workspace.TryClose())
        {
            return false;
        }

        CloseAudit();
        return true;
    }

    private void OpenWorkspace(AuditWorkspace workspace)
    {
        _workspace?.Workspace.Dispose();
        var viewModel = new WorkspaceViewModel(_services, workspace)
        {
            CloseRequested = CloseAudit,
            NewAuditRequested = CreateAudit
        };

        _workspace = viewModel;
        CurrentPage = viewModel;
        _isAuditsSelected = true;
        _isSettingsSelected = false;
        RaisePropertyChanged(nameof(IsAuditsSelected));
        RaisePropertyChanged(nameof(IsSettingsSelected));
        RaisePropertyChanged(nameof(IsAuditOpen));
        RaisePropertyChanged(nameof(AuditContext));
        RaisePropertyChanged(nameof(ShowAuditContext));
    }
}
