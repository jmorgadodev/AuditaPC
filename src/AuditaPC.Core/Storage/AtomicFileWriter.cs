using System.Security.Cryptography;

namespace AuditaPC.Core.Storage;

/// <summary>Operaciones de archivo mínimas, sustituibles en pruebas de fallos.</summary>
public interface IFileSystem
{
    bool FileExists(string path);
    bool DirectoryExists(string path);
    void CreateDirectory(string path);
    byte[] ReadAllBytes(string path);
    void WriteAllBytesDurable(string path, ReadOnlySpan<byte> bytes);
    void Move(string source, string destination, bool overwrite);
    void Replace(string source, string destination, string? backup);
    void Copy(string source, string destination, bool overwrite);
    void Delete(string path);
    IEnumerable<string> EnumerateFiles(string directory, string pattern, SearchOption option);
    IEnumerable<string> EnumerateDirectories(string directory);
    DateTimeOffset GetLastWriteTime(string path);
}

public sealed class PhysicalFileSystem : IFileSystem
{
    public static PhysicalFileSystem Instance { get; } = new();

    public bool FileExists(string path) => File.Exists(path);
    public bool DirectoryExists(string path) => Directory.Exists(path);
    public void CreateDirectory(string path) => Directory.CreateDirectory(path);
    public byte[] ReadAllBytes(string path) => File.ReadAllBytes(path);
    public void Move(string source, string destination, bool overwrite) => File.Move(source, destination, overwrite);
    public void Replace(string source, string destination, string? backup) => File.Replace(source, destination, backup);
    public void Copy(string source, string destination, bool overwrite) => File.Copy(source, destination, overwrite);
    public void Delete(string path) => File.Delete(path);
    public IEnumerable<string> EnumerateFiles(string directory, string pattern, SearchOption option) =>
        Directory.EnumerateFiles(directory, pattern, option);
    public IEnumerable<string> EnumerateDirectories(string directory) =>
        Directory.EnumerateDirectories(directory);
    public DateTimeOffset GetLastWriteTime(string path) => File.GetLastWriteTimeUtc(path);

    public void WriteAllBytesDurable(string path, ReadOnlySpan<byte> bytes)
    {
        try
        {
            using var stream = new FileStream(
                path,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                64 * 1024,
                FileOptions.WriteThrough);
            stream.Write(bytes);
            stream.Flush(flushToDisk: true);
        }
        catch (IOException)
        {
            // Algunos dispositivos extraíbles no admiten WriteThrough. Se reintenta
            // sin esa opción; la verificación posterior sigue siendo obligatoria.
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            stream.Write(bytes);
            stream.Flush(flushToDisk: true);
        }
    }
}

/// <summary>
/// Escritura segura de archivos: temporal en el mismo volumen, reemplazo,
/// copia de seguridad y verificación por hash. Si la verificación falla se
/// intenta restaurar el archivo anterior.
/// </summary>
public sealed class AtomicFileWriter
{
    private readonly IFileSystem _fileSystem;

    public AtomicFileWriter(IFileSystem? fileSystem = null)
    {
        _fileSystem = fileSystem ?? PhysicalFileSystem.Instance;
    }

    public void WriteAtomic(string path, ReadOnlySpan<byte> content, bool keepBackup = true)
    {
        var directory = Path.GetDirectoryName(path);
        if (string.IsNullOrEmpty(directory))
        {
            throw new ArgumentException("La ruta no tiene directorio.", nameof(path));
        }

        _fileSystem.CreateDirectory(directory);
        var tempPath = path + ".tmp-" + Guid.NewGuid().ToString("N");
        var backupPath = path + ".bak";
        var hadPrevious = _fileSystem.FileExists(path);

        try
        {
            _fileSystem.WriteAllBytesDurable(tempPath, content);

            if (hadPrevious)
            {
                if (keepBackup)
                {
                    _fileSystem.Copy(path, backupPath, overwrite: true);
                }

                try
                {
                    _fileSystem.Replace(tempPath, path, keepBackup ? backupPath : null);
                }
                catch (Exception ex) when (ex is IOException or PlatformNotSupportedException or UnauthorizedAccessException)
                {
                    // Reemplazo no disponible en el sistema de archivos (por ejemplo
                    // algunas implementaciones FAT). Se usa renombrado con sobrescritura.
                    _fileSystem.Move(tempPath, path, overwrite: true);
                }
            }
            else
            {
                _fileSystem.Move(tempPath, path, overwrite: false);
            }

            var written = _fileSystem.ReadAllBytes(path);
            if (!CryptographicOperations.FixedTimeEquals(SHA256.HashData(written), SHA256.HashData(content)))
            {
                throw new IOException("La verificación posterior a la escritura no coincide.");
            }
        }
        catch
        {
            TryDelete(tempPath);
            if (hadPrevious && _fileSystem.FileExists(backupPath))
            {
                try
                {
                    _fileSystem.Copy(backupPath, path, overwrite: true);
                }
                catch
                {
                    // El error original es más informativo; la copia de seguridad
                    // permanece en disco para recuperación manual.
                }
            }

            throw;
        }
        finally
        {
            TryDelete(tempPath);
        }
    }

    private void TryDelete(string path)
    {
        try
        {
            if (_fileSystem.FileExists(path))
            {
                _fileSystem.Delete(path);
            }
        }
        catch
        {
            // Limpieza best-effort.
        }
    }
}
