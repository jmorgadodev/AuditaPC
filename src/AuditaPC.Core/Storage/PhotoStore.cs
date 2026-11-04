using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using AuditaPC.Core.Model;

namespace AuditaPC.Core.Storage;

/// <summary>
/// Guarda la foto original y genera una copia de visualización más liviana.
/// El informe HTML incrusta solo la copia de visualización; el original queda
/// intacto en la carpeta de la auditoría.
/// </summary>
public sealed class PhotoStore
{
    public const int DisplayMaxDimension = 1600;
    public const long MaxOriginalBytes = 40L * 1024 * 1024;
    public const long MaxImagePixels = 60_000_000;

    // System.Drawing/GDI+ no ofrece decodificación WebP consistente en todas
    // las versiones de Windows; se limita a formatos que la aplicación valida.
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tif", ".tiff"
    };

    private readonly IFileSystem _fileSystem;

    public PhotoStore(IFileSystem? fileSystem = null)
    {
        _fileSystem = fileSystem ?? PhysicalFileSystem.Instance;
    }

    public static bool IsSupported(string path) =>
        AllowedExtensions.Contains(Path.GetExtension(path));

    public PhotoAttachment AddPhoto(
        string auditFolder,
        string computerId,
        string sourcePath,
        string? description = null)
    {
        if (!_fileSystem.FileExists(sourcePath))
        {
            throw new FileNotFoundException("No se encontró la imagen seleccionada.", sourcePath);
        }

        var extension = Path.GetExtension(sourcePath);
        if (!AllowedExtensions.Contains(extension))
        {
            throw new InvalidOperationException(
                "Formato de imagen no admitido. Usa JPG, PNG, BMP, GIF o TIFF.");
        }

        var originalBytes = _fileSystem.ReadAllBytes(sourcePath);
        if (originalBytes.LongLength > MaxOriginalBytes)
        {
            throw new InvalidOperationException(
                $"La imagen supera el límite de {MaxOriginalBytes / (1024 * 1024)} MB.");
        }

        var photo = new PhotoAttachment
        {
            OriginalFileName = Path.GetFileName(sourcePath),
            ContentType = ContentTypeFor(extension),
            SizeBytes = originalBytes.LongLength,
            Sha256 = JsonFileStore.ComputeSha256(originalBytes),
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            AddedAt = DateTimeOffset.Now
        };

        var relativeFolder = $"{AuditRepository.PhotosFolderName}/{PathGuard.SanitizeFileName(computerId, "equipo")}";
        var originalName = $"{photo.Id}{extension.ToLowerInvariant()}";
        var displayName = $"{photo.Id}_vista.jpg";
        var originalRelative = $"{relativeFolder}/{originalName}";
        var displayRelative = $"{relativeFolder}/{displayName}";

        var originalPath = Path.Combine(
            auditFolder, originalRelative.Replace('/', Path.DirectorySeparatorChar));
        var displayPath = Path.Combine(
            auditFolder, displayRelative.Replace('/', Path.DirectorySeparatorChar));

        _fileSystem.CreateDirectory(Path.GetDirectoryName(originalPath)!);

        try
        {
            // Se valida y genera primero la vista. Si el archivo no es una
            // imagen legible, no queda una copia original huérfana.
            var (width, height) = WriteDisplayCopy(originalBytes, displayPath);

            new AtomicFileWriter(_fileSystem).WriteAtomic(originalPath, originalBytes);
            var verified = _fileSystem.ReadAllBytes(originalPath);
            if (!string.Equals(
                    JsonFileStore.ComputeSha256(verified),
                    photo.Sha256,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new IOException("La verificación de la foto original no coincide.");
            }

            photo.Width = width;
            photo.Height = height;
            photo.StoredPath = originalRelative;
            photo.DisplayPath = displayRelative;
            return photo;
        }
        catch (Exception ex) when (ex is ArgumentException or OutOfMemoryException or ExternalException)
        {
            TryDelete(originalPath);
            TryDelete(displayPath);
            throw new InvalidOperationException(
                "El archivo seleccionado no contiene una imagen compatible o está dañado.", ex);
        }
        catch
        {
            TryDelete(originalPath);
            TryDelete(displayPath);
            throw;
        }
    }

    public string ResolvePath(string auditFolder, string relativePath) =>
        PathGuard.ResolveInside(auditFolder, relativePath);

    public void DeletePhotoFiles(string auditFolder, PhotoAttachment photo)
    {
        foreach (var relative in new[] { photo.StoredPath, photo.DisplayPath })
        {
            if (string.IsNullOrWhiteSpace(relative))
            {
                continue;
            }

            try
            {
                var path = ResolvePath(auditFolder, relative);
                if (_fileSystem.FileExists(path))
                {
                    _fileSystem.Delete(path);
                }
            }
            catch
            {
                // El registro se elimina igualmente; un archivo residual no
                // afecta a la integridad de las capturas.
            }
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
            // La limpieza no debe ocultar el error original.
        }
    }

    private static (int Width, int Height) WriteDisplayCopy(byte[] originalBytes, string displayPath)
    {
        using var sourceStream = new MemoryStream(originalBytes, writable: false);
        using var source = Image.FromStream(sourceStream, useEmbeddedColorManagement: false, validateImageData: true);
        ApplyOrientation(source);

        var pixelCount = (long)source.Width * source.Height;
        if (source.Width <= 0 || source.Height <= 0 || pixelCount > MaxImagePixels)
        {
            throw new InvalidOperationException(
                $"La imagen supera el límite de {MaxImagePixels:N0} píxeles o tiene dimensiones inválidas.");
        }

        var scale = Math.Min(
            1d,
            (double)DisplayMaxDimension / Math.Max(source.Width, source.Height));
        var width = Math.Max(1, (int)Math.Round(source.Width * scale));
        var height = Math.Max(1, (int)Math.Round(source.Height * scale));

        var directory = Path.GetDirectoryName(displayPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var target = new Bitmap(width, height, PixelFormat.Format24bppRgb);
        using (var graphics = Graphics.FromImage(target))
        {
            graphics.CompositingQuality = CompositingQuality.HighQuality;
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.SmoothingMode = SmoothingMode.HighQuality;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.Clear(Color.White);
            graphics.DrawImage(source, 0, 0, width, height);
        }

        var encoder = GetEncoder(ImageFormat.Jpeg) ?? throw new InvalidOperationException(
            "No se encontró el codificador JPEG del sistema.");
        using var parameters = new EncoderParameters(1);
        using var quality = new EncoderParameter(Encoder.Quality, 82L);
        parameters.Param[0] = quality;

        var temp = displayPath + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            target.Save(temp, encoder, parameters);
            if (File.Exists(displayPath))
            {
                File.Delete(displayPath);
            }

            File.Move(temp, displayPath);
        }
        finally
        {
            if (File.Exists(temp))
            {
                File.Delete(temp);
            }
        }

        return (source.Width, source.Height);
    }

    private static void ApplyOrientation(Image image)
    {
        const int OrientationPropertyId = 0x0112;
        try
        {
            if (!image.PropertyIdList.Contains(OrientationPropertyId))
            {
                return;
            }

            var item = image.GetPropertyItem(OrientationPropertyId);
            if (item?.Value is not { Length: > 0 } bytes)
            {
                return;
            }

            var value = bytes[0];
            var rotate = value switch
            {
                3 => RotateFlipType.Rotate180FlipNone,
                6 => RotateFlipType.Rotate90FlipNone,
                8 => RotateFlipType.Rotate270FlipNone,
                _ => RotateFlipType.RotateNoneFlipNone
            };

            if (rotate != RotateFlipType.RotateNoneFlipNone)
            {
                image.RotateFlip(rotate);
            }
        }
        catch
        {
            // La orientación es cosmética: si no puede aplicarse, se conserva la imagen.
        }
    }

    private static ImageCodecInfo? GetEncoder(ImageFormat format) =>
        ImageCodecInfo.GetImageEncoders().FirstOrDefault(codec => codec.FormatID == format.Guid);

    private static string ContentTypeFor(string extension) => extension.ToLowerInvariant() switch
    {
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".bmp" => "image/bmp",
        ".gif" => "image/gif",
        ".tif" or ".tiff" => "image/tiff",
        _ => "application/octet-stream"
    };
}
