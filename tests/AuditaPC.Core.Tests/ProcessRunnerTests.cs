using AuditaPC.Core.Inventory.Collectors;

namespace AuditaPC.Core.Tests;

public sealed class ProcessRunnerTests
{
    [Fact]
    public async Task RunAsync_propagates_cancellation_and_terminates_child_process_tree()
    {
        var marker = Path.Combine(
            Path.GetTempPath(), $"auditapc-process-{Guid.NewGuid():N}.txt");
        using var cancellation = new CancellationTokenSource();

        var arguments = $"/c ping 127.0.0.1 -n 5 > nul & echo terminado > \"{marker}\"";
        var run = ProcessRunner.RunAsync(
            "cmd.exe", arguments, TimeSpan.FromSeconds(15), cancellation.Token);

        await Task.Delay(250);
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => run.WaitAsync(TimeSpan.FromSeconds(3)));

        // Si cmd.exe o su proceso hijo siguieran vivos, escribirían el marcador.
        await Task.Delay(TimeSpan.FromSeconds(6));
        Assert.False(File.Exists(marker));
    }

    [Fact]
    public async Task RunAsync_reports_timeout_separately_from_cancellation()
    {
        var result = await ProcessRunner.RunAsync(
            "cmd.exe",
            "/c ping 127.0.0.1 -n 5 > nul",
            TimeSpan.FromMilliseconds(150));

        Assert.True(result.Started);
        Assert.True(result.TimedOut);
        Assert.Null(result.ExitCode);
        Assert.Equal("Tiempo de espera agotado.", result.Error);
    }

    [Fact]
    public async Task RunAsync_returns_output_for_a_successful_local_command()
    {
        var result = await ProcessRunner.RunAsync(
            "cmd.exe", "/c echo AuditaPC", TimeSpan.FromSeconds(5));

        Assert.True(result.Started);
        Assert.False(result.TimedOut);
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("AuditaPC", result.StandardOutput);
    }
}
