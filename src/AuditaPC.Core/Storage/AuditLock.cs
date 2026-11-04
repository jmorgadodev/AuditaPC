namespace AuditaPC.Core.Storage;

public sealed class AuditLockedException : Exception
{
    public AuditLockedException(string message) : base(message)
    {
    }

    public AuditLockedException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

/// <summary>
/// Bloqueo exclusivo de una auditoría para impedir dos escrituras simultáneas.
/// El bloqueo lo libera el sistema operativo si el proceso termina de forma anormal.
/// </summary>
public sealed class AuditLock : IDisposable
{
    private FileStream? _stream;
    private readonly string _path;

    private AuditLock(FileStream stream, string path)
    {
        _stream = stream;
        _path = path;
    }

    public static AuditLock Acquire(string auditRoot)
    {
        Directory.CreateDirectory(auditRoot);
        var path = Path.Combine(auditRoot, ".audit.lock");

        try
        {
            var stream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            var payload = System.Text.Encoding.UTF8.GetBytes(
                $"pid={Environment.ProcessId}; iniciado={DateTimeOffset.Now:O}\n");
            stream.SetLength(0);
            stream.Write(payload);
            stream.Flush(flushToDisk: true);
            return new AuditLock(stream, path);
        }
        catch (IOException ex)
        {
            throw new AuditLockedException(
                "La auditoría está abierta en otra ventana de AuditaPC o el dispositivo no permite el bloqueo. " +
                "Cierra la otra ventana e inténtalo de nuevo.", ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new AuditLockedException(
                "No hay permisos para escribir en la carpeta de la auditoría. " +
                "Comprueba que el pendrive no esté protegido contra escritura.", ex);
        }
    }

    public void Dispose()
    {
        var stream = _stream;
        _stream = null;
        if (stream is null)
        {
            return;
        }

        try
        {
            stream.Dispose();
        }
        finally
        {
            try
            {
                if (File.Exists(_path))
                {
                    File.Delete(_path);
                }
            }
            catch
            {
                // El archivo de bloqueo puede quedar; no impide volver a abrir.
            }
        }
    }
}
