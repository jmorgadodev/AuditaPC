using System.Text;
using System.Text.Json;
using AuditaPC.Core.Reporting;
using AuditaPC.Core.Storage;

namespace AuditaPC.Core.Tests;

public sealed class ReportWriterTests
{
    [Fact]
    public void Generate_creates_self_contained_html_csv_and_json()
    {
        using var temp = new TempDirectory();
        var repository = new AuditRepository();
        using var workspace = repository.CreateAudit(
            temp.Path,
            "Empresa Ficticia SpA",
            "Técnico de prueba",
            "Auditoría ficticia",
            new DateOnly(2026, 1, 15));

        workspace.Manifest.VisitSummary = "Levantamiento de prueba realizado en terreno.";
        workspace.Manifest.ConnectivityNotes = "La sede no dispone de acceso a Internet durante la visita.";
        workspace.Manifest.ModulesNotes = "Se revisaron los módulos de gestión y contabilidad.";
        workspace.Manifest.ConditionNotes = "Los equipos permanecen operativos.";
        workspace.SaveManifest();

        var computer = TestData.CreateComputer();
        workspace.SaveComputer(computer);
        workspace.SaveCapture(computer, TestData.CreateSnapshot("EQUIPO-DEMO"), "Captura ficticia");
        computer.DisplayName = "CAMBIO-NO-GUARDADO-EN-MEMORIA";

        var bundle = new ReportWriter().Generate(workspace, new[] { computer });

        Assert.True(File.Exists(bundle.HtmlPath));
        Assert.Equal("Empresa Ficticia SpA", bundle.CompanyName);
        Assert.Equal(workspace.Manifest.Id, bundle.AuditId);
        Assert.Equal(new DateOnly(2026, 1, 15), bundle.AuditDate);
        Assert.Equal(1, bundle.ComputerCount);
        Assert.Single(bundle.IndividualHtmlPaths);
        Assert.True(File.Exists(bundle.IndividualHtmlPaths[0]));
        var individualHtml = File.ReadAllText(bundle.IndividualHtmlPaths[0], Encoding.UTF8);
        Assert.Contains("EQUIPO-DEMO", individualHtml);
        Assert.True(File.Exists(bundle.SummaryCsvPath));
        Assert.True(File.Exists(bundle.SoftwareCsvPath));
        Assert.True(File.Exists(bundle.ChecksCsvPath));
        Assert.True(File.Exists(bundle.JsonPath));
        Assert.Empty(bundle.Warnings);

        var html = File.ReadAllText(bundle.HtmlPath, Encoding.UTF8);
        Assert.DoesNotContain("CAMBIO-NO-GUARDADO-EN-MEMORIA", html);
        Assert.StartsWith("<!DOCTYPE html>", html);
        Assert.Contains("Empresa Ficticia SpA", html);
        var decodedHtml = System.Net.WebUtility.HtmlDecode(html);
        Assert.Contains("Técnico de prueba", decodedHtml);
        Assert.Contains("Notas de terreno", decodedHtml);
        Assert.Contains("Inventario consolidado", decodedHtml);
        Assert.Contains("Hallazgos y verificaciones pendientes", decodedHtml);
        Assert.Contains("Plan de acción y recomendaciones", decodedHtml);
        Assert.Contains("Abrir ficha", decodedHtml);
        Assert.Contains("La sede no dispone de acceso a Internet", decodedHtml);
        Assert.Contains("Se revisaron los módulos de gestión", decodedHtml);
        Assert.Contains("EQUIPO-DEMO", html);
        Assert.Contains("SERIE-DEMO-0001", html);
        Assert.Contains("Programa de Ejemplo", html);
        Assert.Contains("No recopila contraseñas", decodedHtml);
        Assert.Contains("Content-Security-Policy", html);
        Assert.Contains("inventory-search", html);
        Assert.Contains("inventory-state", html);
        Assert.Contains("export-inventory", html);
        Assert.Contains("#resumen", html);
        Assert.Contains("#inventario", html);
        Assert.Contains("#fichas", html);
        Assert.Contains("id=\"open-report-guide\"", html);
        Assert.Contains("<dialog class=\"report-guide\" id=\"report-guide\"", html);
        Assert.Contains("id=\"close-report-guide\"", html);
        Assert.Contains("guide.showModal()", html);
        Assert.Contains(".nav,.report-guide,", html);
        Assert.True(html.IndexOf("</main>", StringComparison.Ordinal) <
                    html.IndexOf("<dialog class=\"report-guide\"", StringComparison.Ordinal),
                    "La guía de lectura debe estar fuera del contenido principal del informe.");
        Assert.DoesNotContain("href=\"#alcance\"", html);
        Assert.Contains("window.print()", html);
        Assert.Contains("<script>", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<script src=", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("http://", html);
        Assert.DoesNotContain("https://", html);
    }

    [Fact]
    public void Csv_reports_use_bom_semicolon_and_spanish_headers()
    {
        using var temp = new TempDirectory();
        var repository = new AuditRepository();
        using var workspace = repository.CreateAudit(
            temp.Path, "Empresa Ficticia SpA", "Técnico de prueba",
            startDate: new DateOnly(2026, 1, 15));

        var computer = TestData.CreateComputer();
        workspace.SaveComputer(computer);
        workspace.SaveCapture(computer, TestData.CreateSnapshot("EQUIPO-DEMO"));

        var bundle = new ReportWriter().Generate(workspace, new[] { computer });

        var summaryBytes = File.ReadAllBytes(bundle.SummaryCsvPath);
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, summaryBytes.Take(3).ToArray());
        var summary = File.ReadAllText(bundle.SummaryCsvPath, Encoding.UTF8);
        Assert.Contains(";", summary);
        Assert.Contains("EQUIPO-DEMO", summary);
        Assert.Contains("Fabricante Ficticio", summary);

        var checks = File.ReadAllText(bundle.ChecksCsvPath, Encoding.UTF8);
        Assert.Contains("Estado", checks);
        Assert.Contains("Verificado", checks);
        Assert.Contains("Requiere administrador", checks);
    }

    [Fact]
    public void Generate_creates_one_individual_html_per_computer()
    {
        using var temp = new TempDirectory();
        using var workspace = new AuditRepository().CreateAudit(
            temp.Path, "Empresa Ficticia SpA", "Técnico de prueba",
            startDate: new DateOnly(2026, 1, 15));

        var first = TestData.CreateComputer("PC-UNO");
        var second = TestData.CreateComputer("PC-DOS");
        workspace.SaveComputer(first);
        workspace.SaveCapture(first, TestData.CreateSnapshot("PC-UNO"));
        workspace.SaveComputer(second);
        workspace.SaveCapture(second, TestData.CreateSnapshot("PC-DOS"));

        var bundle = new ReportWriter().Generate(workspace, new[] { first, second });
        var consolidatedHtml = File.ReadAllText(bundle.HtmlPath, Encoding.UTF8);

        Assert.Contains("PC-UNO", consolidatedHtml);
        Assert.Contains("PC-DOS", consolidatedHtml);
        Assert.Contains("#fichas", consolidatedHtml);
        Assert.Equal(2, bundle.IndividualHtmlPaths.Count);
        var firstHtml = File.ReadAllText(
            bundle.IndividualHtmlPaths.Single(path => path.Contains("PC-UNO", StringComparison.Ordinal)),
            Encoding.UTF8);
        var secondHtml = File.ReadAllText(
            bundle.IndividualHtmlPaths.Single(path => path.Contains("PC-DOS", StringComparison.Ordinal)),
            Encoding.UTF8);

        Assert.Contains("PC-UNO", firstHtml);
        Assert.DoesNotContain("PC-DOS", firstHtml);
        Assert.Contains("PC-DOS", secondHtml);
        Assert.DoesNotContain("PC-UNO", secondHtml);
    }

    [Fact]
    public void Reports_from_different_companies_are_separate_and_identifiable()
    {
        using var temp = new TempDirectory();
        var repository = new AuditRepository();
        using var firstWorkspace = repository.CreateAudit(
            temp.Path, "Empresa Alfa", "Técnico A", startDate: new DateOnly(2026, 11, 4));
        using var secondWorkspace = repository.CreateAudit(
            temp.Path, "Empresa Beta", "Técnico B", startDate: new DateOnly(2026, 11, 4));

        var firstComputer = TestData.CreateComputer("PC-ALFA");
        var secondComputer = TestData.CreateComputer("PC-BETA");
        firstWorkspace.SaveComputer(firstComputer);
        firstWorkspace.SaveCapture(firstComputer, TestData.CreateSnapshot("PC-ALFA"));
        secondWorkspace.SaveComputer(secondComputer);
        secondWorkspace.SaveCapture(secondComputer, TestData.CreateSnapshot("PC-BETA"));

        var writer = new ReportWriter();
        var firstBundle = writer.Generate(firstWorkspace, firstWorkspace.LoadComputers());
        var secondBundle = writer.Generate(secondWorkspace, secondWorkspace.LoadComputers());
        Assert.Throws<InvalidOperationException>(
            () => writer.Generate(secondWorkspace, new[] { firstComputer }));
        var firstHtml = File.ReadAllText(firstBundle.HtmlPath, Encoding.UTF8);
        var secondHtml = File.ReadAllText(secondBundle.HtmlPath, Encoding.UTF8);

        Assert.NotEqual(firstWorkspace.FolderPath, secondWorkspace.FolderPath);
        Assert.NotEqual(firstBundle.HtmlPath, secondBundle.HtmlPath);
        Assert.Contains("Empresa-Alfa", Path.GetFileName(firstBundle.HtmlPath));
        Assert.Contains("Empresa-Beta", Path.GetFileName(secondBundle.HtmlPath));
        Assert.Contains(firstBundle.AuditId[..8], Path.GetFileName(firstBundle.HtmlPath));
        Assert.Contains(secondBundle.AuditId[..8], Path.GetFileName(secondBundle.HtmlPath));
        Assert.Equal("Empresa Alfa", firstBundle.CompanyName);
        Assert.Equal("Empresa Beta", secondBundle.CompanyName);
        Assert.Equal(1, firstBundle.ComputerCount);
        Assert.Equal(1, secondBundle.ComputerCount);
        Assert.Contains("Empresa Alfa", firstHtml);
        Assert.Contains("PC-ALFA", firstHtml);
        Assert.DoesNotContain("Empresa Beta", firstHtml);
        Assert.DoesNotContain("PC-BETA", firstHtml);
        Assert.Contains("Empresa Beta", secondHtml);
        Assert.Contains("PC-BETA", secondHtml);
        Assert.DoesNotContain("Empresa Alfa", secondHtml);
        Assert.DoesNotContain("PC-ALFA", secondHtml);
    }

    [Fact]
    public void Csv_export_neutralizes_formula_like_manual_values()
    {
        using var temp = new TempDirectory();
        using var workspace = new AuditRepository().CreateAudit(
            temp.Path, "Empresa Ficticia SpA", "Técnico de prueba",
            startDate: new DateOnly(2026, 1, 15));

        var computer = TestData.CreateComputer();
        computer.Manual.AssetCode = "=1+1";
        workspace.SaveComputer(computer);
        workspace.SaveCapture(computer, TestData.CreateSnapshot("EQUIPO-DEMO"));

        var bundle = new ReportWriter().Generate(workspace, new[] { computer });
        var csv = File.ReadAllText(bundle.SummaryCsvPath, Encoding.UTF8);

        Assert.Contains(";'=1+1;", csv);
        Assert.DoesNotContain(";=1+1;", csv);
    }

    [Fact]
    public void Json_export_contains_manifest_summary_and_captures()
    {
        using var temp = new TempDirectory();
        var repository = new AuditRepository();
        using var workspace = repository.CreateAudit(
            temp.Path, "Empresa Ficticia SpA", "Técnico de prueba",
            startDate: new DateOnly(2026, 1, 15));

        var computer = TestData.CreateComputer();
        workspace.SaveComputer(computer);
        workspace.SaveCapture(computer, TestData.CreateSnapshot("EQUIPO-DEMO"));

        var bundle = new ReportWriter().Generate(workspace, new[] { computer });
        using var document = JsonDocument.Parse(File.ReadAllBytes(bundle.JsonPath));
        var root = document.RootElement;

        Assert.Equal("1.0", root.GetProperty("schemaVersion").GetString());
        Assert.Equal("Empresa Ficticia SpA", root.GetProperty("manifest").GetProperty("companyName").GetString());
        Assert.Equal(1, root.GetProperty("summary").GetProperty("computerCount").GetInt32());
        Assert.Single(root.GetProperty("computers").EnumerateArray());
        Assert.NotEmpty(root.GetProperty("computers")[0].GetProperty("captures").EnumerateArray());
    }

    [Fact]
    public void Report_files_are_not_overwritten_between_runs()
    {
        using var temp = new TempDirectory();
        var repository = new AuditRepository();
        using var workspace = repository.CreateAudit(
            temp.Path, "Empresa Ficticia SpA", "Técnico de prueba",
            startDate: new DateOnly(2026, 1, 15));

        var computer = TestData.CreateComputer();
        workspace.SaveComputer(computer);
        workspace.SaveCapture(computer, TestData.CreateSnapshot("EQUIPO-DEMO"));

        var writer = new ReportWriter();
        var first = writer.Generate(workspace, new[] { computer });
        var second = writer.Generate(workspace, new[] { computer });

        Assert.NotEqual(first.HtmlPath, second.HtmlPath);
        Assert.True(File.Exists(first.HtmlPath));
        Assert.True(File.Exists(second.HtmlPath));
    }
}
