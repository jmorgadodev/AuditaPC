using AuditaPC.Core.Model;
using AuditaPC.Core.Storage;

namespace AuditaPC.Core.Reporting;

/// <summary>
/// Genera el paquete de informes de una auditoría en su carpeta <c>reports/</c>.
/// Los archivos se escriben de forma atómica y nunca sobrescriben un informe anterior.
/// </summary>
public sealed class ReportWriter
{
    private readonly IFileSystem _fileSystem;

    public ReportWriter(IFileSystem? fileSystem = null)
    {
        _fileSystem = fileSystem ?? PhysicalFileSystem.Instance;
    }

    public ReportBundle Generate(
        AuditWorkspace workspace,
        IReadOnlyList<ComputerRecord> computers,
        ReportOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(computers);

        options ??= new ReportOptions();

        // Nunca se aceptan fichas de otra auditoría ni objetos antiguos en memoria.
        // Se resuelven los IDs contra el expediente abierto y se leen sus datos actuales desde disco.
        var persistedComputers = workspace.LoadComputers();
        var requestedIds = computers
            .Select(computer => computer.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var persistedById = persistedComputers.ToDictionary(
            computer => computer.Id,
            StringComparer.OrdinalIgnoreCase);
        var foreignIds = requestedIds
            .Where(id => !persistedById.ContainsKey(id))
            .ToList();

        if (foreignIds.Count > 0)
        {
            throw new InvalidOperationException(
                "No se pueden generar informes con equipos que no pertenecen a la auditoría abierta. " +
                "Cierra el expediente anterior y abre o crea la auditoría correcta.");
        }

        var currentComputers = persistedComputers
            .Where(computer => requestedIds.Contains(computer.Id))
            .ToList();
        var data = AuditReportData.Build(workspace, currentComputers, options.EmbedPhotos);
        var generatedAt = data.GeneratedAt;
        var companySlug = PathGuard.Slugify(data.Manifest.CompanyName, "empresa", 32);
        var auditId = PathGuard.SanitizeFileName(data.Manifest.Id, "auditoria", 32);
        var baseName = $"informe-{companySlug}-auditoria-{auditId[..Math.Min(8, auditId.Length)]}-" +
                       $"{generatedAt:yyyyMMdd-HHmmss}-{Guid.NewGuid().ToString("N")[..8]}";

        var reportsFolder = workspace.ReportsFolder;
        Directory.CreateDirectory(reportsFolder);

        var htmlPath = Path.Combine(reportsFolder, baseName + ".html");
        var summaryPath = Path.Combine(reportsFolder, baseName + "-inventario.csv");
        var softwarePath = Path.Combine(reportsFolder, baseName + "-software.csv");
        var checksPath = Path.Combine(reportsFolder, baseName + "-comprobaciones.csv");
        var jsonPath = Path.Combine(reportsFolder, baseName + ".json");

        var writer = new AtomicFileWriter(_fileSystem);
        writer.WriteAtomic(htmlPath, System.Text.Encoding.UTF8.GetBytes(
            HtmlReportGenerator.Build(data, options)));

        var individualHtmlPaths = new List<string>();
        foreach (var computer in data.Computers)
        {
            var individualData = new AuditReportData
            {
                Manifest = data.Manifest,
                GeneratedAt = data.GeneratedAt,
                AppVersion = data.AppVersion,
                Warnings = data.Warnings.ToList(),
                Computers = new List<ComputerReport> { computer }
            };

            var computerSlug = PathGuard.Slugify(computer.DisplayName, "equipo", 32);
            var computerId = PathGuard.SanitizeFileName(computer.Record.Id, "equipo", 32);
            var individualPath = Path.Combine(
                reportsFolder, $"{baseName}-equipo-{computerSlug}-{computerId}.html");

            writer.WriteAtomic(individualPath, System.Text.Encoding.UTF8.GetBytes(
                HtmlReportGenerator.Build(individualData, options)));
            individualHtmlPaths.Add(individualPath);
        }

        writer.WriteAtomic(summaryPath, CsvReportGenerator.BuildSummary(data));
        writer.WriteAtomic(softwarePath, CsvReportGenerator.BuildSoftware(data));
        writer.WriteAtomic(checksPath, CsvReportGenerator.BuildChecks(data));
        writer.WriteAtomic(jsonPath, JsonReportGenerator.Build(workspace, data));

        return new ReportBundle
        {
            CompanyName = data.Manifest.CompanyName,
            AuditId = data.Manifest.Id,
            AuditDate = data.Manifest.StartDate,
            ComputerCount = data.Computers.Count,
            HtmlPath = htmlPath,
            IndividualHtmlPaths = individualHtmlPaths,
            SummaryCsvPath = summaryPath,
            SoftwareCsvPath = softwarePath,
            ChecksCsvPath = checksPath,
            JsonPath = jsonPath,
            GeneratedAt = generatedAt,
            Warnings = data.Warnings.ToList()
        };
    }
}
