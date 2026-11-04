using AuditaPC.Core.Model;
using AuditaPC.Core.Storage;

namespace AuditaPC.Core.Tests;

public sealed class JsonFileStoreTests
{
    [Fact]
    public void Write_and_Read_roundtrip()
    {
        using var temp = new TempDirectory();
        var path = temp.Combine("computer.json");
        var store = new JsonFileStore();
        var record = new ComputerRecord
        {
            DisplayName = "EQUIPO-DEMO",
            Manual = new ManualRecord { AssetCode = "ACT-01" }
        };

        store.Write(path, record);
        var loaded = store.Read<ComputerRecord>(path);

        Assert.Equal(record.Id, loaded.Id);
        Assert.Equal("EQUIPO-DEMO", loaded.DisplayName);
        Assert.Equal("ACT-01", loaded.Manual.AssetCode);
        Assert.True(store.Exists(path));
    }

    [Fact]
    public void TryRead_reports_missing_and_invalid_files_without_throwing()
    {
        using var temp = new TempDirectory();
        var store = new JsonFileStore();
        var missing = temp.Combine("missing.json");

        Assert.False(store.TryRead<ComputerRecord>(missing, out _, out var missingError));
        Assert.Contains("no existe", missingError, StringComparison.OrdinalIgnoreCase);

        var invalid = temp.Combine("invalid.json");
        File.WriteAllText(invalid, "{ esto no es json }");
        Assert.False(store.TryRead<ComputerRecord>(invalid, out _, out var invalidError));
        Assert.NotNull(invalidError);
    }

    [Fact]
    public void Read_invalid_json_throws_InvalidDataException()
    {
        using var temp = new TempDirectory();
        var path = temp.Combine("invalid.json");
        File.WriteAllText(path, "no-json");
        Assert.Throws<InvalidDataException>(() => new JsonFileStore().Read<ComputerRecord>(path));
    }

    [Fact]
    public void ComputeSha256_is_lowercase_hex()
    {
        var hash = JsonFileStore.ComputeSha256("abc"u8);
        Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", hash);
        Assert.Equal(hash.ToLowerInvariant(), hash);
    }
}
