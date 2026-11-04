using AuditaPC.Core.Model;
using AuditaPC.Core.Storage;

namespace AuditaPC.Core.Tests;

public sealed class AuditRepositoryTests
{
    [Fact]
    public void CreateAudit_creates_independent_folder_and_layout()
    {
        using var temp = new TempDirectory();
        var repository = new AuditRepository();
        using var workspace = repository.CreateAudit(
            temp.Path,
            "Empresa Ficticia SpA",
            "Técnico de prueba",
            "Auditoría ficticia",
            new DateOnly(2026, 1, 15));

        Assert.True(Directory.Exists(workspace.FolderPath));
        Assert.StartsWith("Empresa-Ficticia-SpA__2026-01-15__", Path.GetFileName(workspace.FolderPath));
        Assert.True(File.Exists(Path.Combine(workspace.FolderPath, "audit.json")));
        Assert.True(Directory.Exists(workspace.CapturesFolder));
        Assert.True(Directory.Exists(workspace.PhotosFolder));
        Assert.True(Directory.Exists(workspace.ReportsFolder));
        Assert.Equal("Empresa Ficticia SpA", workspace.Manifest.CompanyName);
    }

    [Fact]
    public void SaveCapture_is_immutable_and_preserves_history()
    {
        using var temp = new TempDirectory();
        var repository = new AuditRepository();
        using var workspace = repository.CreateAudit(
            temp.Path, "Empresa Ficticia SpA", "Técnico de prueba",
            startDate: new DateOnly(2026, 1, 15));

        var computer = TestData.CreateComputer();
        workspace.SaveComputer(computer);

        var first = workspace.SaveCapture(computer, TestData.CreateSnapshot("EQUIPO-DEMO"), "Primera captura");
        var firstPath = Path.Combine(workspace.FolderPath, "captures", first.Id + ".json");
        var firstBytes = File.ReadAllBytes(firstPath);

        var second = workspace.SaveCapture(computer, TestData.CreateSnapshot("EQUIPO-DEMO"), "Segunda captura");
        var secondPath = Path.Combine(workspace.FolderPath, "captures", second.Id + ".json");

        Assert.NotEqual(first.Id, second.Id);
        Assert.True(File.Exists(firstPath));
        Assert.True(File.Exists(secondPath));
        Assert.Equal(firstBytes, File.ReadAllBytes(firstPath));
        Assert.Equal(second.Id, computer.CurrentCaptureId);
        Assert.Equal(2, computer.Captures.Count);
        Assert.All(computer.Captures, reference => Assert.False(string.IsNullOrWhiteSpace(reference.Sha256)));
    }

    [Fact]
    public void LoadComputers_and_catalog_return_saved_data()
    {
        using var temp = new TempDirectory();
        var repository = new AuditRepository();
        using var workspace = repository.CreateAudit(
            temp.Path, "Empresa Ficticia SpA", "Técnico de prueba",
            startDate: new DateOnly(2026, 1, 15));

        var computer = TestData.CreateComputer();
        workspace.SaveComputer(computer);
        workspace.SaveCapture(computer, TestData.CreateSnapshot("EQUIPO-DEMO"));

        var loaded = workspace.LoadComputers();
        Assert.Single(loaded);
        Assert.Equal(computer.Id, loaded[0].Id);
        Assert.Single(loaded[0].Captures);

        var catalog = repository.ListAudits(temp.Path);
        Assert.Single(catalog);
        Assert.True(catalog[0].IsValid);
        Assert.Equal(1, catalog[0].ComputerCount);
        Assert.Equal("1 equipo", catalog[0].ComputerCountText);
        Assert.Equal("0 equipos", new AuditCatalogItem { ComputerCount = 0 }.ComputerCountText);
        Assert.Equal("Empresa Ficticia SpA", catalog[0].Manifest!.CompanyName);
    }

    [Fact]
    public void Recover_relinks_orphan_capture_without_deleting_it()
    {
        using var temp = new TempDirectory();
        var repository = new AuditRepository();
        using var workspace = repository.CreateAudit(
            temp.Path, "Empresa Ficticia SpA", "Técnico de prueba",
            startDate: new DateOnly(2026, 1, 15));

        var orphan = new CaptureRecord
        {
            Id = Guid.NewGuid().ToString("N"),
            ComputerId = Guid.NewGuid().ToString("N"),
            CapturedAt = new DateTimeOffset(2026, 1, 15, 11, 0, 0, TimeSpan.Zero),
            Inventory = TestData.CreateSnapshot("EQUIPO-HUERFANO")
        };
        var orphanPath = Path.Combine(workspace.FolderPath, "captures", orphan.Id + ".json");
        new JsonFileStore().Write(orphanPath, orphan);
        var orphanBytes = File.ReadAllBytes(orphanPath);

        var report = workspace.Recover();

        Assert.Equal(1, report.OrphanCaptures);
        Assert.Equal(1, report.RecoveredCaptures);
        Assert.Equal(1, report.CreatedComputers);
        Assert.Empty(report.Errors);
        Assert.True(File.Exists(orphanPath));
        Assert.Equal(orphanBytes, File.ReadAllBytes(orphanPath));

        var computers = workspace.LoadComputers();
        Assert.Single(computers);
        Assert.Equal(orphan.ComputerId, computers[0].Id);
        Assert.Equal(orphan.Id, computers[0].CurrentCaptureId);
    }

    [Fact]
    public void OpenAudit_reopens_saved_computers_and_capture_history()
    {
        using var temp = new TempDirectory();
        var repository = new AuditRepository();
        string auditFolder;

        using (var created = repository.CreateAudit(
                   temp.Path, "Empresa Reapertura", "Técnico QA",
                   "Prueba de persistencia", new DateOnly(2026, 11, 4)))
        {
            var computer = TestData.CreateComputer("PC-REAPERTURA");
            created.SaveComputer(computer);
            created.SaveCapture(computer, TestData.CreateSnapshot("PC-REAPERTURA"), "Primera visita");
            created.SaveCapture(computer, TestData.CreateSnapshot("PC-REAPERTURA"), "Segunda visita");
            created.Manifest.VisitSummary = "Resumen persistido de la visita.";
            created.SaveManifest();
            auditFolder = created.FolderPath;
        }

        using var reopened = repository.OpenAudit(auditFolder);
        var loaded = reopened.LoadComputers();

        Assert.Equal("Empresa Reapertura", reopened.Manifest.CompanyName);
        Assert.Equal("Prueba de persistencia", reopened.Manifest.Notes);
        Assert.Equal("Resumen persistido de la visita.", reopened.Manifest.VisitSummary);
        var record = Assert.Single(loaded);
        Assert.Equal("PC-REAPERTURA", record.DisplayName);
        Assert.Equal(2, record.Captures.Count);
        Assert.Equal(record.Captures[^1].Id, record.CurrentCaptureId);
        var latestCapture = reopened.LoadCapture(record.Captures[^1]);
        Assert.Equal("Segunda visita", latestCapture.Note);
        Assert.Equal("PC-REAPERTURA", latestCapture.Inventory.Environment.MachineName);
    }

    [Fact]
    public void OpenAudit_rejects_folder_without_manifest()
    {
        using var temp = new TempDirectory();
        Assert.Throws<InvalidDataException>(() => new AuditRepository().OpenAudit(temp.Path));
    }

    [Fact]
    public void BuildFolderName_uses_slug_date_and_short_id()
    {
        var name = AuditRepository.BuildFolderName(
            "Niño & Compañía", new DateOnly(2026, 3, 9), "0123456789abcdef");
        Assert.Equal("Nino-Compania__2026-03-09__01234567", name);
    }
}
