using System.Security.Cryptography;

namespace AuditaPC.Core.Storage;

public sealed class JsonFileStore
{
    private readonly IFileSystem _fileSystem;
    private readonly AtomicFileWriter _writer;

    public JsonFileStore(IFileSystem? fileSystem = null)
    {
        _fileSystem = fileSystem ?? PhysicalFileSystem.Instance;
        _writer = new AtomicFileWriter(_fileSystem);
    }

    public bool Exists(string path) => _fileSystem.FileExists(path);

    public byte[] ReadBytes(string path) => _fileSystem.ReadAllBytes(path);

    public T Read<T>(string path)
    {
        var bytes = _fileSystem.ReadAllBytes(path);
        try
        {
            var value = JsonDefaults.Deserialize<T>(bytes);
            if (value is null)
            {
                throw new InvalidDataException($"El archivo {Path.GetFileName(path)} no contiene datos válidos.");
            }

            return value;
        }
        catch (Exception ex) when (ex is not InvalidDataException)
        {
            throw new InvalidDataException(
                $"No se pudo interpretar {Path.GetFileName(path)} como JSON válido.", ex);
        }
    }

    public bool TryRead<T>(string path, out T? value, out string? error)
    {
        value = default;
        error = null;
        try
        {
            if (!_fileSystem.FileExists(path))
            {
                error = "El archivo no existe.";
                return false;
            }

            value = Read<T>(path);
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public void Write<T>(string path, T value)
    {
        var bytes = JsonDefaults.SerializeToUtf8Bytes(value);

        // Se valida el contenido serializado antes de tocar el archivo anterior.
        var check = JsonDefaults.Deserialize<T>(bytes);
        if (check is null)
        {
            throw new InvalidDataException("La serialización produjo un documento vacío.");
        }

        _writer.WriteAtomic(path, bytes);
    }

    public void WriteBytesAtomic(string path, ReadOnlySpan<byte> bytes) =>
        _writer.WriteAtomic(path, bytes);

    public static string ComputeSha256(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    public static string ComputeFileSha256(IFileSystem fileSystem, string path) =>
        ComputeSha256(fileSystem.ReadAllBytes(path));
}
