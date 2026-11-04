using System.Diagnostics;
using System.Text;

namespace AuditaPC.Core.Inventory.Collectors;

public sealed class ProcessResult
{
    public bool Started { get; init; }
    public int? ExitCode { get; init; }
    public string StandardOutput { get; init; } = string.Empty;
    public string StandardError { get; init; } = string.Empty;
    public bool TimedOut { get; init; }
    public string? Error { get; init; }
}

/// <summary>Ejecución de utilidades locales de diagnóstico, sin shell ni entrada del usuario.</summary>
public static class ProcessRunner
{
    public static async Task<ProcessResult> RunAsync(
        string fileName,
        string arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        try
        {
            using var process = new Process { StartInfo = startInfo };
            if (!process.Start())
            {
                return new ProcessResult { Started = false, Error = "El proceso no pudo iniciarse." };
            }

            try
            {
                var stdoutTask = process.StandardOutput.ReadToEndAsync();
                var stderrTask = process.StandardError.ReadToEndAsync();
                var exitTask = process.WaitForExitAsync(cancellationToken);
                var completed = await Task.WhenAny(exitTask, Task.Delay(timeout, cancellationToken))
                    .ConfigureAwait(false);

                // La cancelación del técnico no equivale a un timeout. Hay que
                // terminar también los procesos hijos y propagar la cancelación.
                if (cancellationToken.IsCancellationRequested)
                {
                    TryKillProcessTree(process);
                    cancellationToken.ThrowIfCancellationRequested();
                }

                if (completed != exitTask)
                {
                    TryKillProcessTree(process);
                    return new ProcessResult
                    {
                        Started = true,
                        TimedOut = true,
                        StandardOutput = await SafeReadAsync(stdoutTask),
                        StandardError = await SafeReadAsync(stderrTask),
                        Error = "Tiempo de espera agotado."
                    };
                }

                await exitTask.ConfigureAwait(false);
                return new ProcessResult
                {
                    Started = true,
                    ExitCode = process.ExitCode,
                    StandardOutput = await SafeReadAsync(stdoutTask),
                    StandardError = await SafeReadAsync(stderrTask)
                };
            }
            catch (OperationCanceledException)
            {
                TryKillProcessTree(process);
                throw;
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new ProcessResult
            {
                Started = false,
                Error = ex.Message
            };
        }
    }

    private static void TryKillProcessTree(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // El proceso pudo terminar entre la comprobación y el cierre.
        }
    }

    private static async Task<string> SafeReadAsync(Task<string> task)
    {
        try
        {
            return await task.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
        }
        catch
        {
            return string.Empty;
        }
    }
}
