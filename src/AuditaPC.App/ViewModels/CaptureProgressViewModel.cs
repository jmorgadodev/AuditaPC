using System.Collections.ObjectModel;
using AuditaPC.App.Services;
using AuditaPC.Core.Inventory;
using AuditaPC.Core.Model;

namespace AuditaPC.App.ViewModels;

public sealed class CollectorRow : ObservableObject
{
    private CollectorRunState _state;
    private CheckStatus? _status;
    private long _elapsedMs;
    private string? _message;

    public CollectorRow(string id, string title)
    {
        Id = id;
        Title = title;
    }

    public string Id { get; }
    public string Title { get; }

    public CollectorRunState State
    {
        get => _state;
        set
        {
            if (Set(ref _state, value))
            {
                RaisePropertyChanged(nameof(StateText));
            }
        }
    }

    public CheckStatus? Status
    {
        get => _status;
        set
        {
            if (Set(ref _status, value))
            {
                RaisePropertyChanged(nameof(StatusText));
            }
        }
    }

    public long ElapsedMs
    {
        get => _elapsedMs;
        set
        {
            if (Set(ref _elapsedMs, value))
            {
                RaisePropertyChanged(nameof(ElapsedText));
            }
        }
    }

    public string? Message
    {
        get => _message;
        set => Set(ref _message, value);
    }

    public string StateText => State switch
    {
        CollectorRunState.Pending => "En espera",
        CollectorRunState.Running => "Consultando",
        CollectorRunState.Completed => "Completado",
        CollectorRunState.Failed => "Error",
        CollectorRunState.TimedOut => "Tiempo agotado",
        CollectorRunState.Canceled => "Cancelado",
        _ => "—"
    };

    public string StatusText => Status?.ToDisplayText() ?? string.Empty;

    public string ElapsedText => ElapsedMs <= 0 ? string.Empty : $"{ElapsedMs} ms";

    public bool HasStatus => Status is not null;
}

public sealed class CaptureProgressViewModel : ObservableObject
{
    private readonly AppServices _services;
    private readonly CancellationTokenSource _cancellation = new();

    private bool _isRunning = true;
    private bool _isFinished;
    private string _statusMessage = "Iniciando consultas locales…";
    private int _completed;
    private int _total;

    public CaptureProgressViewModel(AppServices services)
    {
        _services = services;
        foreach (var collector in services.Runner.Collectors)
        {
            Collectors.Add(new CollectorRow(collector.Id, collector.Title));
        }

        _total = Collectors.Count;
        CancelCommand = new RelayCommand(Cancel, () => IsRunning);
    }

    public ObservableCollection<CollectorRow> Collectors { get; } = new();
    public RelayCommand CancelCommand { get; }
    public InventorySnapshot? Snapshot { get; private set; }

    public bool IsRunning
    {
        get => _isRunning;
        private set
        {
            if (!Set(ref _isRunning, value))
            {
                return;
            }

            CancelCommand.RaiseCanExecuteChanged();
            RaisePropertyChanged(nameof(IsFinished));
        }
    }

    public bool IsFinished
    {
        get => _isFinished;
        private set
        {
            if (!Set(ref _isFinished, value))
            {
                return;
            }

            RaisePropertyChanged(nameof(IsRunning));
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => Set(ref _statusMessage, value);
    }

    public int Completed
    {
        get => _completed;
        private set
        {
            if (Set(ref _completed, value))
            {
                RaisePropertyChanged(nameof(ProgressText));
            }
        }
    }

    public string ProgressText => $"{Completed} de {_total} consultas finalizadas";

    public async Task StartAsync()
    {
        try
        {
            var context = CollectorContext.Create(
                _cancellation.Token,
                _services.Settings.EnableOnlineLicenseChecks);
            var progress = new Progress<CollectorProgress>(Update);
            Snapshot = await _services.Runner.RunAsync(context, progress);
            StatusMessage = "Consultas finalizadas. Revisa los resultados antes de guardar.";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Captura cancelada. No se guardará ninguna instantánea.";
        }
        catch (Exception ex)
        {
            StatusMessage = "No se pudo completar la captura: " + ex.Message;
        }
        finally
        {
            IsRunning = false;
            IsFinished = true;
            CancelCommand.RaiseCanExecuteChanged();
        }
    }

    public void Cancel()
    {
        if (!IsRunning)
        {
            return;
        }

        StatusMessage = "Cancelando… las consultas en curso pueden tardar unos segundos.";
        try
        {
            _cancellation.Cancel();
        }
        catch
        {
            // Sin efecto adicional.
        }
    }

    private void Update(CollectorProgress progress)
    {
        var row = Collectors.FirstOrDefault(item => item.Id == progress.CollectorId);
        if (row is null)
        {
            return;
        }

        row.State = progress.State;
        row.Status = progress.Status;
        row.ElapsedMs = progress.ElapsedMs;
        row.Message = progress.Message;

        if (progress.State is CollectorRunState.Completed or CollectorRunState.Failed
            or CollectorRunState.TimedOut or CollectorRunState.Canceled)
        {
            Completed = Collectors.Count(item => item.State is CollectorRunState.Completed
                or CollectorRunState.Failed or CollectorRunState.TimedOut or CollectorRunState.Canceled);
        }
        else if (progress.State == CollectorRunState.Running)
        {
            StatusMessage = $"Consultando: {progress.Title}";
        }
    }
}
