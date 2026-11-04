using AuditaPC.Core.Storage;

namespace AuditaPC.Core.Tests;

public sealed class AppPathResolverTests
{
    [Fact]
    public void Resolve_uses_writable_executable_directory_for_portable_data()
    {
        using var temp = new TempDirectory();
        var paths = AppPathResolver.Resolve(temp.Path, temp.Combine("local"));

        Assert.True(paths.IsPortable);
        Assert.True(paths.SettingsArePortable);
        Assert.Equal(Path.Combine(temp.Path, "Auditorias"), paths.AuditsRoot);
        Assert.True(Directory.Exists(paths.AuditsRoot));
    }

    [Fact]
    public void Resolve_honors_configured_audits_root()
    {
        using var temp = new TempDirectory();
        var customRoot = temp.Combine("mis-auditorias");
        var settings = new AppSettings
        {
            AuditsRoot = customRoot,
            TechnicianName = "Técnico de prueba"
        };

        var paths = AppPathResolver.Resolve(temp.Path, temp.Combine("local"), settings);

        Assert.Equal(Path.GetFullPath(customRoot), paths.AuditsRoot);
        Assert.Equal("mis-auditorias", settings.AuditsRoot);
        Assert.Same(settings, paths.Settings);
        Assert.True(paths.IsPortable);
        Assert.True(Directory.Exists(customRoot));
    }

    [Fact]
    public void Resolve_relative_root_follows_the_executable_when_drive_letter_changes()
    {
        using var firstLocation = new TempDirectory();
        using var secondLocation = new TempDirectory();
        var settings = new AppSettings { AuditsRoot = "Auditorias" };

        var first = AppPathResolver.Resolve(firstLocation.Path, firstLocation.Combine("local"), settings);
        var second = AppPathResolver.Resolve(secondLocation.Path, secondLocation.Combine("local"), settings);

        Assert.Equal(
            Path.Combine(firstLocation.Path, "Auditorias"),
            first.AuditsRoot);
        Assert.Equal(
            Path.Combine(secondLocation.Path, "Auditorias"),
            second.AuditsRoot);
        Assert.True(first.IsPortable);
        Assert.True(second.IsPortable);
        Assert.Equal("Auditorias", settings.AuditsRoot);
    }

    [Fact]
    public void Resolve_migrates_the_previous_default_audits_folder()
    {
        using var temp = new TempDirectory();
        var settings = new AppSettings
        {
            AuditsRoot = Path.Combine("Datos", "Auditorias")
        };

        var paths = AppPathResolver.Resolve(temp.Path, temp.Combine("local"), settings);

        Assert.Equal(Path.Combine(temp.Path, "Auditorias"), paths.AuditsRoot);
        Assert.Equal("Auditorias", settings.AuditsRoot);
    }

    [Fact]
    public void SaveSettings_and_LoadSettings_roundtrip()
    {
        using var temp = new TempDirectory();
        var path = temp.Combine("AuditaPC.config.json");
        var settings = new AppSettings
        {
            AuditsRoot = temp.Combine("auditorias"),
            TechnicianName = "Técnico de prueba"
        };

        AppPathResolver.SaveSettings(path, settings);
        var loaded = AppPathResolver.LoadSettings(path);

        Assert.Equal(settings.AuditsRoot, loaded.AuditsRoot);
        Assert.Equal("Técnico de prueba", loaded.TechnicianName);
        Assert.True(File.Exists(path));
    }

    [Fact]
    public void LoadSettings_with_invalid_file_returns_defaults_and_warning()
    {
        using var temp = new TempDirectory();
        var path = temp.Combine("AuditaPC.config.json");
        File.WriteAllText(path, "esto no es json");
        var warnings = new List<string>();

        var settings = AppPathResolver.LoadSettings(path, warnings);

        Assert.Null(settings.AuditsRoot);
        Assert.Single(warnings);
    }

    [Fact]
    public void CanWrite_is_true_for_temp_directory()
    {
        using var temp = new TempDirectory();
        Assert.True(AppPathResolver.CanWrite(temp.Path));
    }
}
