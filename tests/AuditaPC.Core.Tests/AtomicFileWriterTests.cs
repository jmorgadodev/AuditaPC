using System.Text;
using AuditaPC.Core.Storage;

namespace AuditaPC.Core.Tests;

public sealed class AtomicFileWriterTests
{
    [Fact]
    public void WriteAtomic_creates_file_and_leaves_no_temporary_files()
    {
        using var temp = new TempDirectory();
        var path = temp.Combine("datos", "audit.json");
        var content = Encoding.UTF8.GetBytes("{\"valor\":1}");

        new AtomicFileWriter().WriteAtomic(path, content);

        Assert.True(File.Exists(path));
        Assert.Equal(content, File.ReadAllBytes(path));
        Assert.Empty(Directory.EnumerateFiles(Path.GetDirectoryName(path)!, "*.tmp-*"));
    }

    [Fact]
    public void WriteAtomic_overwrites_and_keeps_backup()
    {
        using var temp = new TempDirectory();
        var path = temp.Combine("audit.json");
        var first = Encoding.UTF8.GetBytes("primero");
        var second = Encoding.UTF8.GetBytes("segundo");

        var writer = new AtomicFileWriter();
        writer.WriteAtomic(path, first);
        writer.WriteAtomic(path, second);

        Assert.Equal(second, File.ReadAllBytes(path));
        Assert.True(File.Exists(path + ".bak"));
        Assert.Equal(first, File.ReadAllBytes(path + ".bak"));
    }

    [Fact]
    public void WriteAtomic_restores_previous_file_when_verification_fails()
    {
        using var temp = new TempDirectory();
        var path = temp.Combine("audit.json");
        var first = Encoding.UTF8.GetBytes("contenido-anterior");
        var second = Encoding.UTF8.GetBytes("contenido-nuevo");
        var fileSystem = new CorruptingFileSystem(path);
        var writer = new AtomicFileWriter(fileSystem);

        writer.WriteAtomic(path, first);
        fileSystem.CorruptNextRead = true;

        Assert.Throws<IOException>(() => writer.WriteAtomic(path, second));
        Assert.Equal(first, File.ReadAllBytes(path));
    }

    private sealed class CorruptingFileSystem : IFileSystem
    {
        private readonly string _targetPath;

        public CorruptingFileSystem(string targetPath)
        {
            _targetPath = targetPath;
        }

        public bool CorruptNextRead { get; set; }

        public bool FileExists(string path) => File.Exists(path);
        public bool DirectoryExists(string path) => Directory.Exists(path);
        public void CreateDirectory(string path) => Directory.CreateDirectory(path);

        public byte[] ReadAllBytes(string path)
        {
            var bytes = File.ReadAllBytes(path);
            if (CorruptNextRead && string.Equals(path, _targetPath, StringComparison.OrdinalIgnoreCase))
            {
                CorruptNextRead = false;
                bytes[0] ^= 0xFF;
            }

            return bytes;
        }

        public void WriteAllBytesDurable(string path, ReadOnlySpan<byte> bytes)
        {
            using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            stream.Write(bytes);
            stream.Flush(true);
        }

        public void Move(string source, string destination, bool overwrite) =>
            File.Move(source, destination, overwrite);

        public void Replace(string source, string destination, string? backup) =>
            File.Replace(source, destination, backup);

        public void Copy(string source, string destination, bool overwrite) =>
            File.Copy(source, destination, overwrite);

        public void Delete(string path) => File.Delete(path);

        public IEnumerable<string> EnumerateFiles(string directory, string pattern, SearchOption option) =>
            Directory.EnumerateFiles(directory, pattern, option);

        public IEnumerable<string> EnumerateDirectories(string directory) =>
            Directory.EnumerateDirectories(directory);

        public DateTimeOffset GetLastWriteTime(string path) => File.GetLastWriteTimeUtc(path);
    }
}
