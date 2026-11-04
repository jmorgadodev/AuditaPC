using System.Diagnostics;
using System.Security.Principal;
using AuditaPC.Core.Model;

namespace AuditaPC.Core.Inventory;

/// <summary>Contexto de ejecución de una captura. No contiene secretos.</summary>
public sealed class CollectorContext
{
    public CancellationToken CancellationToken { get; init; }
    public bool IsElevated { get; init; }
    public bool EnableOnlineLicenseChecks { get; init; }
    public string UserName { get; init; } = string.Empty;
    public string UserDomain { get; init; } = string.Empty;
    public DateTimeOffset StartedAt { get; init; } = DateTimeOffset.Now;

    public static CollectorContext Create(
        CancellationToken cancellationToken = default,
        bool enableOnlineLicenseChecks = false)
    {
        var isElevated = false;
        var userName = Environment.UserName;
        var userDomain = Environment.UserDomainName;

        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            userName = identity.Name;
            userDomain = Environment.UserDomainName;
            var principal = new WindowsPrincipal(identity);
            isElevated = principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            // El contexto se mantiene con los valores de Environment.
        }

        return new CollectorContext
        {
            CancellationToken = cancellationToken,
            IsElevated = isElevated,
            EnableOnlineLicenseChecks = enableOnlineLicenseChecks,
            UserName = userName,
            UserDomain = userDomain
        };
    }
}

public enum CollectorRunState
{
    Pending,
    Running,
    Completed,
    Failed,
    TimedOut,
    Canceled
}

public sealed class CollectorProgress
{
    public string CollectorId { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public CollectorRunState State { get; init; }
    public string? Message { get; init; }
    public long ElapsedMs { get; init; }
    public CheckStatus? Status { get; init; }
}

/// <summary>Resultado que el colector entrega al orquestador sin mutar la instantánea.</summary>
public sealed class CollectorOutcome
{
    public required string Id { get; init; }
    public required CheckStatus Status { get; init; }
    public required Action<InventorySnapshot> Apply { get; init; }
    public string? Message { get; init; }
}

public interface IInventoryCollector
{
    string Id { get; }
    string Title { get; }
    TimeSpan Timeout { get; }
    Task<CollectorOutcome> CollectAsync(CollectorContext context);
    void ApplyUnavailable(InventorySnapshot target, CheckStatus status, string message, string? errorType);
}

public sealed class InventoryRunner
{
    private readonly IReadOnlyList<IInventoryCollector> _collectors;
    private readonly int _maxConcurrency;

    public InventoryRunner(IEnumerable<IInventoryCollector>? collectors = null, int maxConcurrency = 3)
    {
        _collectors = collectors?.ToList() ?? CreateDefaultCollectors();
        _maxConcurrency = Math.Max(1, maxConcurrency);
    }

    public IReadOnlyList<IInventoryCollector> Collectors => _collectors;

    public static IReadOnlyList<IInventoryCollector> CreateDefaultCollectors() => new IInventoryCollector[]
    {
        new Collectors.SystemIdentityCollector(),
        new Collectors.OperatingSystemCollector(),
        new Collectors.HardwareCollector(),
        new Collectors.StorageCollector(),
        new Collectors.NetworkCollector(),
        new Collectors.SoftwareCollector(),
        new Collectors.OfficeCollector(),
        new Collectors.SecurityCollector()
    };

    public async Task<InventorySnapshot> RunAsync(
        CollectorContext context,
        IProgress<CollectorProgress>? progress = null)
    {
        var snapshot = new InventorySnapshot
        {
            Environment = new CaptureEnvironment
            {
                MachineName = Environment.MachineName,
                UserName = context.UserName,
                UserDomain = context.UserDomain,
                IsElevated = context.IsElevated,
                OnlineLicenseChecksEnabled = context.EnableOnlineLicenseChecks,
                Is64BitOperatingSystem = Environment.Is64BitOperatingSystem,
                Is64BitProcess = Environment.Is64BitProcess,
                OsDescription = Environment.OSVersion.VersionString,
                ProcessArchitecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(),
                StartedAt = DateTimeOffset.Now
            }
        };

        foreach (var collector in _collectors)
        {
            progress?.Report(new CollectorProgress
            {
                CollectorId = collector.Id,
                Title = collector.Title,
                State = CollectorRunState.Pending
            });
        }

        using var gate = new SemaphoreSlim(_maxConcurrency, _maxConcurrency);
        var tasks = _collectors.Select(collector =>
            RunOneAsync(collector, snapshot, context, gate, progress)).ToList();

        await Task.WhenAll(tasks).ConfigureAwait(false);

        // Una cancelación nunca debe producir una instantánea que el técnico
        // pueda guardar como si la captura hubiera finalizado correctamente.
        context.CancellationToken.ThrowIfCancellationRequested();

        snapshot.Environment.FinishedAt = DateTimeOffset.Now;
        return snapshot;
    }

    private static async Task RunOneAsync(
        IInventoryCollector collector,
        InventorySnapshot snapshot,
        CollectorContext context,
        SemaphoreSlim gate,
        IProgress<CollectorProgress>? progress)
    {
        if (context.CancellationToken.IsCancellationRequested)
        {
            collector.ApplyUnavailable(snapshot, CheckStatus.NotVerifiable, "Captura cancelada.", "Canceled");
            progress?.Report(new CollectorProgress
            {
                CollectorId = collector.Id,
                Title = collector.Title,
                State = CollectorRunState.Canceled,
                Message = "Captura cancelada."
            });
            return;
        }

        var acquired = false;
        var stopwatch = Stopwatch.StartNew();
        try
        {
            await gate.WaitAsync(context.CancellationToken).ConfigureAwait(false);
            acquired = true;

            progress?.Report(new CollectorProgress
            {
                CollectorId = collector.Id,
                Title = collector.Title,
                State = CollectorRunState.Running
            });

            using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                context.CancellationToken);
            var collectorContext = new CollectorContext
            {
                CancellationToken = linkedCancellation.Token,
                IsElevated = context.IsElevated,
                EnableOnlineLicenseChecks = context.EnableOnlineLicenseChecks,
                UserName = context.UserName,
                UserDomain = context.UserDomain,
                StartedAt = context.StartedAt
            };

            var collectionTask = Task.Run(
                () => collector.CollectAsync(collectorContext), CancellationToken.None);
            var timeoutTask = Task.Delay(collector.Timeout, CancellationToken.None);
            var cancellationTask = Task.Delay(Timeout.Infinite, context.CancellationToken);
            var completed = await Task.WhenAny(
                collectionTask, timeoutTask, cancellationTask).ConfigureAwait(false);

            if (context.CancellationToken.IsCancellationRequested || completed == cancellationTask)
            {
                linkedCancellation.Cancel();
                ObserveBackgroundTask(collectionTask);
                collector.ApplyUnavailable(
                    snapshot, CheckStatus.NotVerifiable, "Captura cancelada.", "Canceled");
                stopwatch.Stop();
                progress?.Report(new CollectorProgress
                {
                    CollectorId = collector.Id,
                    Title = collector.Title,
                    State = CollectorRunState.Canceled,
                    ElapsedMs = stopwatch.ElapsedMilliseconds,
                    Message = "Captura cancelada."
                });
                return;
            }

            // Solo se acepta el resultado si el colector fue la tarea que ganó
            // la carrera. Comprobar IsCompleted aquí permite aceptar un resultado
            // tardío si termina entre WhenAny y esta condición.
            if (completed == collectionTask)
            {
                var outcome = await collectionTask.ConfigureAwait(false);
                outcome.Apply(snapshot);
                stopwatch.Stop();
                progress?.Report(new CollectorProgress
                {
                    CollectorId = collector.Id,
                    Title = collector.Title,
                    State = CollectorRunState.Completed,
                    Status = outcome.Status,
                    ElapsedMs = stopwatch.ElapsedMilliseconds,
                    Message = outcome.Message
                });
                return;
            }

            linkedCancellation.Cancel();
            ObserveBackgroundTask(collectionTask);
            collector.ApplyUnavailable(
                snapshot,
                CheckStatus.Timeout,
                $"La consulta excedió el tiempo máximo de {collector.Timeout.TotalSeconds:0} s.",
                "Timeout");
            stopwatch.Stop();
            progress?.Report(new CollectorProgress
            {
                CollectorId = collector.Id,
                Title = collector.Title,
                State = CollectorRunState.TimedOut,
                Status = CheckStatus.Timeout,
                ElapsedMs = stopwatch.ElapsedMilliseconds,
                Message = $"Tiempo máximo excedido ({collector.Timeout.TotalSeconds:0} s)."
            });
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            collector.ApplyUnavailable(snapshot, CheckStatus.NotVerifiable, "Captura cancelada.", "Canceled");
            progress?.Report(new CollectorProgress
            {
                CollectorId = collector.Id,
                Title = collector.Title,
                State = CollectorRunState.Canceled,
                ElapsedMs = stopwatch.ElapsedMilliseconds,
                Message = "Captura cancelada."
            });
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            var message = SanitizeMessage(ex);
            collector.ApplyUnavailable(snapshot, CheckStatus.QueryError, message, ex.GetType().Name);
            progress?.Report(new CollectorProgress
            {
                CollectorId = collector.Id,
                Title = collector.Title,
                State = CollectorRunState.Failed,
                Status = CheckStatus.QueryError,
                ElapsedMs = stopwatch.ElapsedMilliseconds,
                Message = message
            });
        }
        finally
        {
            if (acquired)
            {
                gate.Release();
            }
        }
    }

    private static void ObserveBackgroundTask(Task task)
    {
        _ = task.ContinueWith(
            completed => _ = completed.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private static string SanitizeMessage(Exception exception)
    {
        var message = exception.Message;
        if (string.IsNullOrWhiteSpace(message))
        {
            message = "Error desconocido durante la consulta.";
        }

        if (message.Length > 500)
        {
            message = message[..500] + "…";
        }

        return message;
    }
}
