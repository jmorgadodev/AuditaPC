using AuditaPC.Core.Inventory;
using AuditaPC.Core.Model;

namespace AuditaPC.Core.Tests;

public sealed class InventoryRunnerTests
{
    [Fact]
    public async Task RunAsync_does_not_return_a_snapshot_when_capture_is_canceled()
    {
        using var cancellation = new CancellationTokenSource();
        var collector = new CancellationAwareCollector();
        var runner = new InventoryRunner(new[] { collector }, maxConcurrency: 1);
        var context = new CollectorContext
        {
            CancellationToken = cancellation.Token,
            UserName = "tecnico-prueba",
            UserDomain = "EJEMPLO"
        };

        var run = runner.RunAsync(context);
        await collector.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
    }

    [Fact]
    public async Task RunAsync_marks_a_collector_timeout_without_claiming_success()
    {
        var collector = new SlowCollector();
        var runner = new InventoryRunner(new[] { collector }, maxConcurrency: 1);
        var context = new CollectorContext
        {
            UserName = "tecnico-prueba",
            UserDomain = "EJEMPLO"
        };

        var snapshot = await runner.RunAsync(context);

        Assert.Equal(CheckStatus.Timeout, snapshot.Identity.Status);
        Assert.False(collector.Finished.Task.IsCompleted);
        collector.Release.TrySetResult();
        await collector.Finished.Task.WaitAsync(TimeSpan.FromSeconds(2));
    }

    private sealed class CancellationAwareCollector : IInventoryCollector
    {
        public string Id => "identity";
        public string Title => "Colector de prueba";
        public TimeSpan Timeout => TimeSpan.FromSeconds(5);
        public TaskCompletionSource Started { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<CollectorOutcome> CollectAsync(CollectorContext context)
        {
            Started.TrySetResult();
            await Task.Delay(System.Threading.Timeout.Infinite, context.CancellationToken);
            return new CollectorOutcome
            {
                Id = Id,
                Status = CheckStatus.Verified,
                Apply = snapshot => snapshot.Identity = new CategoryResult<IdentityData>
                {
                    Id = Id,
                    Title = Title,
                    Status = CheckStatus.Verified,
                    Data = new IdentityData()
                }
            };
        }

        public void ApplyUnavailable(
            InventorySnapshot target,
            CheckStatus status,
            string message,
            string? errorType)
        {
            target.Identity = CategoryResult<IdentityData>.Unavailable(
                Id, Title, status, message, errorType);
        }
    }

    private sealed class SlowCollector : IInventoryCollector
    {
        public string Id => "identity";
        public string Title => "Colector lento";
        public TimeSpan Timeout => TimeSpan.FromMilliseconds(100);
        public TaskCompletionSource Release { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Finished { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<CollectorOutcome> CollectAsync(CollectorContext context)
        {
            await Release.Task;
            Finished.TrySetResult();
            return new CollectorOutcome
            {
                Id = Id,
                Status = CheckStatus.Verified,
                Apply = snapshot => snapshot.Identity = new CategoryResult<IdentityData>
                {
                    Id = Id,
                    Title = Title,
                    Status = CheckStatus.Verified,
                    Data = new IdentityData()
                }
            };
        }

        public void ApplyUnavailable(
            InventorySnapshot target,
            CheckStatus status,
            string message,
            string? errorType)
        {
            target.Identity = CategoryResult<IdentityData>.Unavailable(
                Id, Title, status, message, errorType);
        }
    }
}
