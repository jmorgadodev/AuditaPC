using AuditaPC.Core.Model;
using AuditaPC.Core.Storage;

namespace AuditaPC.Core.Reporting;

public sealed class ReportPhoto
{
    public PhotoAttachment Attachment { get; init; } = new();
    public string? DataUri { get; init; }
    public bool Embedded => !string.IsNullOrWhiteSpace(DataUri);
}

public sealed class ComputerReport
{
    public ComputerRecord Record { get; init; } = new();
    public CaptureRecord? CurrentCapture { get; init; }
    public InventorySnapshot? Inventory => CurrentCapture?.Inventory;
    public List<ReportPhoto> Photos { get; init; } = new();

    public string DisplayName => string.IsNullOrWhiteSpace(Record.DisplayName)
        ? "Equipo sin nombre"
        : Record.DisplayName;

    public string? Serial =>
        Inventory?.Identity.Data?.BiosSerial
        ?? Record.Fingerprint.BiosSerial;

    public string? Manufacturer =>
        Inventory?.Identity.Data?.Manufacturer ?? Record.Fingerprint.Manufacturer;

    public string? Model =>
        Inventory?.Identity.Data?.Model ?? Record.Fingerprint.Model;

    public string? OperatingSystem =>
        Inventory?.OperatingSystem.Data?.FriendlyName
        ?? Inventory?.OperatingSystem.Data?.ProductName;

    public IEnumerable<CheckRecord> Checks =>
        Inventory?.AllChecks() ?? Enumerable.Empty<CheckRecord>();
}

public sealed class AuditReportData
{
    public AuditManifest Manifest { get; init; } = new();
    public List<ComputerReport> Computers { get; init; } = new();
    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.Now;
    public string AppVersion { get; init; } = AppInfo.Version;
    public List<string> Warnings { get; init; } = new();

    public IReadOnlyList<CheckRecord> AllChecks =>
        Computers.SelectMany(computer => computer.Checks).ToList();

    public Dictionary<CheckStatus, int> StatusCounts =>
        AllChecks
            .GroupBy(check => check.Status)
            .ToDictionary(group => group.Key, group => group.Count());

    public int ComputersWithProblems =>
        Computers.Count(computer => computer.Checks.Any(check => check.Status == CheckStatus.Problem));

    public int ComputersWithIncompleteData =>
        Computers.Count(computer => computer.Checks.Any(check => check.Status.IsIncomplete()));

    public int TotalPhotos => Computers.Sum(computer => computer.Photos.Count);

    public static AuditReportData Build(
        AuditWorkspace workspace,
        IReadOnlyList<ComputerRecord> computers,
        bool embedPhotos = true)
    {
        var report = new AuditReportData
        {
            Manifest = workspace.Manifest,
            GeneratedAt = DateTimeOffset.Now
        };

        foreach (var computer in computers)
        {
            CaptureRecord? capture = null;
            var reference = computer.CurrentCapture;
            if (reference is not null)
            {
                try
                {
                    capture = workspace.LoadCapture(reference);
                }
                catch (Exception ex)
                {
                    report.Warnings.Add(
                        $"No se pudo leer la captura de «{computer.DisplayName}»: {ex.Message}");
                }
            }

            var photos = new List<ReportPhoto>();
            foreach (var photo in computer.Photos.OrderBy(item => item.AddedAt))
            {
                string? dataUri = null;
                if (embedPhotos && !string.IsNullOrWhiteSpace(photo.DisplayPath))
                {
                    try
                    {
                        var path = PathGuard.ResolveInside(workspace.FolderPath, photo.DisplayPath);
                        if (File.Exists(path))
                        {
                            var bytes = File.ReadAllBytes(path);
                            dataUri = "data:image/jpeg;base64," + Convert.ToBase64String(bytes);
                        }
                    }
                    catch
                    {
                        // Una foto ilegible no debe impedir generar el informe.
                    }
                }

                photos.Add(new ReportPhoto { Attachment = photo, DataUri = dataUri });
            }

            report.Computers.Add(new ComputerReport
            {
                Record = computer,
                CurrentCapture = capture,
                Photos = photos
            });
        }

        return report;
    }
}

public sealed class ReportOptions
{
    public bool EmbedPhotos { get; init; } = true;
    public bool IncludeSoftwareDetail { get; init; } = true;
    public bool IncludeCheckTables { get; init; } = true;
}

public sealed class ReportBundle
{
    public string CompanyName { get; init; } = string.Empty;
    public string AuditId { get; init; } = string.Empty;
    public DateOnly AuditDate { get; init; }
    public int ComputerCount { get; init; }
    public string HtmlPath { get; init; } = string.Empty;
    public List<string> IndividualHtmlPaths { get; init; } = new();
    public string SummaryCsvPath { get; init; } = string.Empty;
    public string SoftwareCsvPath { get; init; } = string.Empty;
    public string ChecksCsvPath { get; init; } = string.Empty;
    public string JsonPath { get; init; } = string.Empty;
    public DateTimeOffset GeneratedAt { get; init; }
    public List<string> Warnings { get; init; } = new();
}
