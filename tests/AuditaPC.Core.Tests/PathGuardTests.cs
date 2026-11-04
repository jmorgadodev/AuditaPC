using AuditaPC.Core.Storage;

namespace AuditaPC.Core.Tests;

public sealed class PathGuardTests
{
    [Fact]
    public void SanitizeFileName_replaces_invalid_characters_and_reserved_names()
    {
        Assert.Equal("Equipo_01", PathGuard.SanitizeFileName("Equipo:01"));
        Assert.Equal("_CON", PathGuard.SanitizeFileName("CON"));
        Assert.Equal("sin-nombre", PathGuard.SanitizeFileName("   "));
        Assert.True(PathGuard.SanitizeFileName(new string('a', 200)).Length <= 80);
    }

    [Fact]
    public void Slugify_removes_accents_and_separators()
    {
        Assert.Equal("Empresa-Ficticia-SpA", PathGuard.Slugify("Empresa Ficticia SpA"));
        Assert.Equal("Nino-Ano", PathGuard.Slugify("Niño Año"));
        Assert.Equal("Empresa", PathGuard.Slugify("***"));
    }

    [Fact]
    public void ResolveInside_accepts_nested_relative_paths()
    {
        using var temp = new TempDirectory();
        var path = PathGuard.ResolveInside(temp.Path, "photos/equipo/foto.jpg");
        Assert.StartsWith(Path.GetFullPath(temp.Path), path, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith("foto.jpg", path, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("../fuera.json")]
    [InlineData("..\\fuera.json")]
    [InlineData("photos/../../fuera.json")]
    public void ResolveInside_rejects_traversal(string relative)
    {
        using var temp = new TempDirectory();
        Assert.Throws<InvalidOperationException>(() => PathGuard.ResolveInside(temp.Path, relative));
    }

    [Fact]
    public void ResolveInside_rejects_absolute_paths()
    {
        using var temp = new TempDirectory();
        Assert.Throws<InvalidOperationException>(
            () => PathGuard.ResolveInside(temp.Path, temp.Combine("otro.json")));
    }

    [Fact]
    public void ToRelativePath_uses_forward_slashes()
    {
        using var temp = new TempDirectory();
        var full = temp.Combine("reports", "informe.html");
        Assert.Equal("reports/informe.html", PathGuard.ToRelativePath(temp.Path, full));
    }

    [Fact]
    public void IsSafeReportFileName_only_accepts_simple_html_names()
    {
        Assert.True(PathGuard.IsSafeReportFileName("informe-2026.html"));
        Assert.False(PathGuard.IsSafeReportFileName("informe.csv"));
        Assert.False(PathGuard.IsSafeReportFileName("../informe.html"));
        Assert.False(PathGuard.IsSafeReportFileName("informe.html.exe"));
    }
}
