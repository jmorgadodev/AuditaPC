using AuditaPC.Core.Model;
using AuditaPC.Core.Storage;

namespace AuditaPC.Core.Reporting;

/// <summary>
/// Exportación JSON consolidada y legible por máquina. Incluye el manifiesto,
/// las fichas, todas las capturas y el resumen de estados. No contiene secretos
/// ni claves de producto.
/// </summary>
public static class JsonReportGenerator
{
    public static byte[] Build(AuditWorkspace workspace, AuditReportData data)
    {
        var export = new AuditJsonExport
        {
            ExportedAt = data.GeneratedAt,
            AppVersion = data.AppVersion,
            Manifest = data.Manifest,
            Summary = new AuditJsonSummary
            {
                ComputerCount = data.Computers.Count,
                ComputersWithProblems = data.ComputersWithProblems,
                ComputersWithIncompleteData = data.ComputersWithIncompleteData,
                PhotoCount = data.TotalPhotos,
                StatusCounts = data.StatusCounts.ToDictionary(
                    pair => pair.Key.ToString(),
                    pair => pair.Value)
            },
            Warnings = data.Warnings.ToList(),
            Computers = new List<ComputerJsonExport>()
        };

        foreach (var computer in data.Computers)
        {
            var captures = new List<CaptureRecord>();
            foreach (var reference in computer.Record.Captures.OrderBy(item => item.CapturedAt))
            {
                try
                {
                    captures.Add(workspace.LoadCapture(reference));
                }
                catch (Exception ex)
                {
                    export.Warnings.Add(
                        $"No se pudo incluir la captura {reference.Id} de «{computer.DisplayName}»: {ex.Message}");
                }
            }

            export.Computers.Add(new ComputerJsonExport
            {
                Record = computer.Record,
                Captures = captures
            });
        }

        return JsonDefaults.SerializeToUtf8Bytes(export);
    }
}

public sealed class AuditJsonExport
{
    public string SchemaVersion { get; set; } = AppInfo.SchemaVersion;
    public DateTimeOffset ExportedAt { get; set; }
    public string AppVersion { get; set; } = AppInfo.Version;
    public AuditManifest Manifest { get; set; } = new();
    public AuditJsonSummary Summary { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public List<ComputerJsonExport> Computers { get; set; } = new();
}

public sealed class AuditJsonSummary
{
    public int ComputerCount { get; set; }
    public int ComputersWithProblems { get; set; }
    public int ComputersWithIncompleteData { get; set; }
    public int PhotoCount { get; set; }
    public Dictionary<string, int> StatusCounts { get; set; } = new();
}

public sealed class ComputerJsonExport
{
    public ComputerRecord Record { get; set; } = new();
    public List<CaptureRecord> Captures { get; set; } = new();
}
