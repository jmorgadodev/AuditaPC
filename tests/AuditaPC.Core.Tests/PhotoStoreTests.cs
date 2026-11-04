using System.Drawing;
using System.Drawing.Imaging;
using AuditaPC.Core.Storage;

namespace AuditaPC.Core.Tests;

public sealed class PhotoStoreTests
{
    [Fact]
    public void AddPhoto_stores_original_and_display_copy_with_verified_hash()
    {
        using var temp = new TempDirectory();
        var source = temp.Combine("origen.png");
        CreateImage(source, 320, 240);

        var store = new PhotoStore();
        var photo = store.AddPhoto(temp.Path, "equipo-1", source, "Etiqueta del equipo");

        var original = Resolve(temp.Path, photo.StoredPath);
        var display = Resolve(temp.Path, photo.DisplayPath);

        Assert.True(File.Exists(original));
        Assert.True(File.Exists(display));
        Assert.Equal("Etiqueta del equipo", photo.Description);
        Assert.Equal(320, photo.Width);
        Assert.Equal(240, photo.Height);
        Assert.Equal(
            photo.Sha256,
            JsonFileStore.ComputeFileSha256(PhysicalFileSystem.Instance, original));
        Assert.Equal("image/png", photo.ContentType);
        Assert.True(File.Exists(display));
        Assert.EndsWith("_vista.jpg", display, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AddPhoto_rejects_unsupported_extensions()
    {
        using var temp = new TempDirectory();
        var source = temp.Combine("documento.txt");
        File.WriteAllText(source, "no es una imagen");

        var exception = Assert.Throws<InvalidOperationException>(
            () => new PhotoStore().AddPhoto(temp.Path, "equipo-1", source));
        Assert.Contains("Formato de imagen", exception.Message);
    }

    [Fact]
    public void AddPhoto_rejects_webp_until_a_consistent_decoder_is_available()
    {
        using var temp = new TempDirectory();
        var source = temp.Combine("imagen.webp");
        File.WriteAllBytes(source, new byte[] { 1, 2, 3 });

        var exception = Assert.Throws<InvalidOperationException>(
            () => new PhotoStore().AddPhoto(temp.Path, "equipo-1", source));
        Assert.Contains("Formato de imagen", exception.Message);
    }

    [Fact]
    public void AddPhoto_rejects_corrupt_images_without_leaving_orphan_files()
    {
        using var temp = new TempDirectory();
        var source = temp.Combine("imagen.jpg");
        File.WriteAllText(source, "esto no es una imagen");

        var exception = Assert.Throws<InvalidOperationException>(
            () => new PhotoStore().AddPhoto(temp.Path, "equipo-1", source));

        Assert.Contains("imagen compatible", exception.Message);
        var photosFolder = Path.Combine(temp.Path, "photos");
        Assert.Empty(Directory.EnumerateFiles(photosFolder, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public void DeletePhotoFiles_removes_both_copies()
    {
        using var temp = new TempDirectory();
        var source = temp.Combine("origen.png");
        CreateImage(source, 64, 64);

        var store = new PhotoStore();
        var photo = store.AddPhoto(temp.Path, "equipo-1", source);
        var original = Resolve(temp.Path, photo.StoredPath);
        var display = Resolve(temp.Path, photo.DisplayPath);

        store.DeletePhotoFiles(temp.Path, photo);

        Assert.False(File.Exists(original));
        Assert.False(File.Exists(display));
    }

    private static string Resolve(string root, string relative) =>
        Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));

    private static void CreateImage(string path, int width, int height)
    {
        using var bitmap = new Bitmap(width, height);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.Clear(Color.CadetBlue);
            graphics.FillRectangle(Brushes.White, width / 4, height / 4, width / 2, height / 2);
        }

        bitmap.Save(path, ImageFormat.Png);
    }
}
