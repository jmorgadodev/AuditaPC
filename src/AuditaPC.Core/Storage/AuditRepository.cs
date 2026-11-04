using System.Security.Cryptography;
using System.Text;
using AuditaPC.Core.Matching;
using AuditaPC.Core.Model;

namespace AuditaPC.Core.Storage;

public sealed class AuditRecoveryReport
{
    public int OrphanCaptures { get; set; }
    public int RecoveredCaptures { get; set; }
    public int CreatedComputers { get; set; }
    public List<string> Messages { get; } = new();
    public List<string> Errors { get; } = new();
}

/// <summary>
/// Acceso a una auditoría independiente en disco. Las capturas son inmutables:
/// guardar una captura nueva nunca sobrescribe ni elimina las anteriores.
/// Todas las rutas persistidas son relativas a la carpeta de la auditoría.
/// </summary>
public sealed class AuditRepository
{
    public const string ManifestFileName = "audit.json";
    public const string ComputersFolderName = "computers";
    public const string CapturesFolderName = "captures";
    public const string PhotosFolderName = "photos";
    public const string ReportsFolderName = "reports";
    public const string ComputerFileName = "computer.json";

    private readonly IFileSystem _fileSystem;
    private readonly JsonFileStore _store;

    public AuditRepository(IFileSystem? fileSystem = null)
    {
        _fileSystem = fileSystem ?? PhysicalFileSystem.Instance;
        _store = new JsonFileStore(_fileSystem);
    }

    public static string BuildFolderName(string companyName, DateOnly date, string auditId)
    {
        var slug = PathGuard.Slugify(companyName, "Empresa");
        var suffix = auditId.Length >= 8 ? auditId[..8] : auditId.PadRight(8, '0');
        return $"{slug}__{date:yyyy-MM-dd}__{suffix}";
    }

    public AuditWorkspace CreateAudit(
        string auditsRoot,
        string companyName,
        string? technician = null,
        string? notes = null,
        DateOnly? startDate = null)
    {
        if (string.IsNullOrWhiteSpace(companyName))
        {
            throw new ArgumentException("El nombre de la empresa es obligatorio.", nameof(companyName));
        }

        var manifest = new AuditManifest
        {
            CompanyName = companyName.Trim(),
            Technician = Clean(technician),
            Notes = Clean(notes),
            StartDate = startDate ?? DateOnly.FromDateTime(DateTime.Today),
            CreatedAt = DateTimeOffset.Now,
            UpdatedAt = DateTimeOffset.Now
        };

        Directory.CreateDirectory(auditsRoot);
        var folder = CreateUniqueFolder(auditsRoot, manifest);
        manifest.UpdatedAt = DateTimeOffset.Now;

        var workspace = new AuditWorkspace(this, folder, manifest);
        workspace.SaveManifest();
        workspace.EnsureLayout();
        return workspace;
    }

    public AuditWorkspace OpenAudit(string auditFolder)
    {
        auditFolder = Path.GetFullPath(auditFolder);
        var manifestPath = Path.Combine(auditFolder, ManifestFileName);
        if (!_fileSystem.FileExists(manifestPath))
        {
            throw new InvalidDataException(
                $"La carpeta seleccionada no contiene {ManifestFileName} y no es una auditoría válida.");
        }

        var manifest = _store.Read<AuditManifest>(manifestPath);
        if (manifest.SchemaNumber > AppInfo.SchemaNumber)
        {
            throw new InvalidDataException(
                "La auditoría fue creada con una versión más reciente de AuditaPC. " +
                "Actualiza la aplicación antes de abrirla para no dañar los datos.");
        }

        var workspace = new AuditWorkspace(this, auditFolder, manifest);
        workspace.EnsureLayout();
        return workspace;
    }

    public IReadOnlyList<AuditCatalogItem> ListAudits(string auditsRoot)
    {
        var items = new List<AuditCatalogItem>();
        if (!_fileSystem.DirectoryExists(auditsRoot))
        {
            return items;
        }

        foreach (var folder in _fileSystem.EnumerateDirectories(auditsRoot))
        {
            var item = new AuditCatalogItem
            {
                FolderPath = folder,
                FolderName = Path.GetFileName(folder),
                LastWriteTime = SafeLastWrite(folder)
            };

            var manifestPath = Path.Combine(folder, ManifestFileName);
            if (!_fileSystem.FileExists(manifestPath))
            {
                item.Error = $"Falta {ManifestFileName}.";
                items.Add(item);
                continue;
            }

            try
            {
                item.Manifest = _store.Read<AuditManifest>(manifestPath);
                item.ComputerCount = CountComputers(folder);
            }
            catch (Exception ex)
            {
                item.Error = ex.Message;
            }

            items.Add(item);
        }

        return items
            .OrderByDescending(item => item.LastWriteTime ?? DateTimeOffset.MinValue)
            .ThenBy(item => item.FolderName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public IReadOnlyList<ComputerRecord> LoadComputers(string auditFolder)
    {
        var computersFolder = Path.Combine(auditFolder, ComputersFolderName);
        var computers = new List<ComputerRecord>();
        if (!_fileSystem.DirectoryExists(computersFolder))
        {
            return computers;
        }

        foreach (var directory in _fileSystem.EnumerateDirectories(computersFolder))
        {
            var path = Path.Combine(directory, ComputerFileName);
            if (!_fileSystem.FileExists(path))
            {
                continue;
            }

            try
            {
                computers.Add(_store.Read<ComputerRecord>(path));
            }
            catch (InvalidDataException)
            {
                // Una ficha dañada no debe impedir abrir el resto de la auditoría.
            }
        }

        return computers
            .OrderBy(computer => computer.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public ComputerRecord? TryLoadComputer(string auditFolder, string computerId)
    {
        var path = ComputerPath(auditFolder, computerId);
        return _store.TryRead<ComputerRecord>(path, out var record, out _) ? record : null;
    }

    public void SaveComputer(string auditFolder, ComputerRecord computer)
    {
        computer.UpdatedAt = DateTimeOffset.Now;
        _store.Write(ComputerPath(auditFolder, computer.Id), computer);
        TouchManifest(auditFolder);
    }

    public CaptureRecord LoadCapture(string auditFolder, string captureId)
    {
        var path = CapturePath(auditFolder, captureId);
        if (!_fileSystem.FileExists(path))
        {
            throw new FileNotFoundException("No se encontró el archivo de la captura.", path);
        }

        return _store.Read<CaptureRecord>(path);
    }

    public CaptureRecord SaveCapture(
        string auditFolder,
        ComputerRecord computer,
        InventorySnapshot snapshot,
        string? note = null,
        DeviceFingerprint? confirmedFingerprint = null)
    {
        var capture = new CaptureRecord
        {
            Id = Guid.NewGuid().ToString("N"),
            ComputerId = computer.Id,
            CapturedAt = snapshot.Environment.FinishedAt == default
                ? DateTimeOffset.Now
                : snapshot.Environment.FinishedAt,
            Note = Clean(note),
            Inventory = snapshot
        };

        var bytes = JsonDefaults.SerializeToUtf8Bytes(capture);
        var relative = $"{CapturesFolderName}/{capture.Id}.json";
        var fullPath = Path.Combine(auditFolder, relative.Replace('/', Path.DirectorySeparatorChar));
        _store.WriteBytesAtomic(fullPath, bytes);

        var sha = JsonFileStore.ComputeSha256(bytes);
        computer.Captures.Add(new CaptureReference
        {
            Id = capture.Id,
            CapturedAt = capture.CapturedAt,
            File = relative,
            Sha256 = sha,
            AppVersion = AppInfo.Version
        });
        computer.CurrentCaptureId = capture.Id;
        computer.Fingerprint = FingerprintFactory.Merge(
            computer.Fingerprint,
            confirmedFingerprint ?? FingerprintFactory.FromSnapshot(snapshot, computer.Manual.AssetCode));

        if (string.IsNullOrWhiteSpace(computer.DisplayName) || computer.DisplayName == "Equipo sin nombre")
        {
            computer.DisplayName = FingerprintFactory.GetDisplayName(snapshot);
        }

        SaveComputer(auditFolder, computer);
        return capture;
    }

    public string GetCapturePath(string auditFolder, CaptureReference reference) =>
        PathGuard.ResolveInside(auditFolder, reference.File);

    /// <summary>
    /// Revisa las capturas del disco y vuelve a enlazar las que no figuren en
    /// ninguna ficha. No borra ni modifica capturas: solo agrega referencias.
    /// </summary>
    public AuditRecoveryReport Recover(string auditFolder)
    {
        var report = new AuditRecoveryReport();
        var capturesFolder = Path.Combine(auditFolder, CapturesFolderName);
        if (!_fileSystem.DirectoryExists(capturesFolder))
        {
            return report;
        }

        var computers = LoadComputers(auditFolder).ToList();
        var known = computers
            .SelectMany(computer => computer.Captures)
            .Select(reference => reference.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var file in _fileSystem.EnumerateFiles(capturesFolder, "*.json", SearchOption.TopDirectoryOnly))
        {
            var id = Path.GetFileNameWithoutExtension(file);
            if (known.Contains(id))
            {
                continue;
            }

            report.OrphanCaptures++;
            CaptureRecord? capture;
            try
            {
                capture = _store.Read<CaptureRecord>(file);
            }
            catch (Exception ex)
            {
                report.Errors.Add($"No se pudo leer {Path.GetFileName(file)}: {ex.Message}");
                continue;
            }

            var computer = computers.FirstOrDefault(c =>
                !string.IsNullOrWhiteSpace(c.Id) &&
                c.Id.Equals(capture.ComputerId, StringComparison.OrdinalIgnoreCase));

            if (computer is null)
            {
                computer = new ComputerRecord
                {
                    Id = string.IsNullOrWhiteSpace(capture.ComputerId)
                        ? Guid.NewGuid().ToString("N")
                        : capture.ComputerId,
                    DisplayName = FingerprintFactory.GetDisplayName(
                        capture.Inventory, "Equipo recuperado " + capture.Id[..8]),
                    CreatedAt = capture.CapturedAt,
                    Fingerprint = FingerprintFactory.FromSnapshot(capture.Inventory)
                };
                computers.Add(computer);
                report.CreatedComputers++;
                report.Messages.Add(
                    $"Se creó la ficha «{computer.DisplayName}» para una captura huérfana.");
            }

            if (computer.Captures.Any(reference => reference.Id == capture.Id))
            {
                continue;
            }

            var bytes = _fileSystem.ReadAllBytes(file);
            computer.Captures.Add(new CaptureReference
            {
                Id = capture.Id,
                CapturedAt = capture.CapturedAt,
                File = $"{CapturesFolderName}/{Path.GetFileName(file)}",
                Sha256 = JsonFileStore.ComputeSha256(bytes),
                AppVersion = capture.AppVersion
            });
            computer.CurrentCaptureId = computer.Captures
                .OrderByDescending(reference => reference.CapturedAt)
                .First()
                .Id;
            report.RecoveredCaptures++;
        }

        foreach (var computer in computers)
        {
            try
            {
                SaveComputer(auditFolder, computer);
            }
            catch (Exception ex)
            {
                report.Errors.Add($"No se pudo guardar la ficha «{computer.DisplayName}»: {ex.Message}");
            }
        }

        return report;
    }

    public string GetComputerFolder(string auditFolder, string computerId) =>
        Path.Combine(auditFolder, ComputersFolderName, PathGuard.SanitizeFileName(computerId, "equipo"));

    internal void WriteManifest(string auditFolder, AuditManifest manifest)
    {
        manifest.UpdatedAt = DateTimeOffset.Now;
        _store.Write(Path.Combine(auditFolder, ManifestFileName), manifest);
    }

    internal string ComputerPath(string auditFolder, string computerId) =>
        Path.Combine(GetComputerFolder(auditFolder, computerId), ComputerFileName);

    internal string CapturePath(string auditFolder, string captureId) =>
        Path.Combine(auditFolder, CapturesFolderName, PathGuard.SanitizeFileName(captureId, "captura") + ".json");

    private string CreateUniqueFolder(string auditsRoot, AuditManifest manifest)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var candidate = Path.Combine(
                auditsRoot,
                BuildFolderName(manifest.CompanyName, manifest.StartDate, manifest.Id));
            if (!_fileSystem.DirectoryExists(candidate) && !_fileSystem.FileExists(candidate))
            {
                _fileSystem.CreateDirectory(candidate);
                return candidate;
            }

            manifest.Id = Guid.NewGuid().ToString("N");
        }

        throw new IOException("No se pudo crear una carpeta única para la auditoría.");
    }

    private int CountComputers(string auditFolder)
    {
        var folder = Path.Combine(auditFolder, ComputersFolderName);
        if (!_fileSystem.DirectoryExists(folder))
        {
            return 0;
        }

        return _fileSystem
            .EnumerateFiles(folder, ComputerFileName, SearchOption.AllDirectories)
            .Count();
    }

    private void TouchManifest(string auditFolder)
    {
        var path = Path.Combine(auditFolder, ManifestFileName);
        if (_store.TryRead<AuditManifest>(path, out var manifest, out _) && manifest is not null)
        {
            manifest.UpdatedAt = DateTimeOffset.Now;
            _store.Write(path, manifest);
        }
    }

    private DateTimeOffset? SafeLastWrite(string folder)
    {
        try
        {
            return _fileSystem.GetLastWriteTime(folder);
        }
        catch
        {
            return null;
        }
    }

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>Auditoría abierta con bloqueo exclusivo mientras la aplicación la usa.</summary>
public sealed class AuditWorkspace : IDisposable
{
    private readonly AuditRepository _repository;
    private readonly AuditLock _lock;
    private bool _disposed;

    internal AuditWorkspace(AuditRepository repository, string folder, AuditManifest manifest)
    {
        _repository = repository;
        FolderPath = Path.GetFullPath(folder);
        Manifest = manifest;
        _lock = AuditLock.Acquire(FolderPath);
    }

    public string FolderPath { get; }
    public AuditManifest Manifest { get; }
    public string CapturesFolder => Path.Combine(FolderPath, AuditRepository.CapturesFolderName);
    public string ReportsFolder => Path.Combine(FolderPath, AuditRepository.ReportsFolderName);
    public string PhotosFolder => Path.Combine(FolderPath, AuditRepository.PhotosFolderName);

    public void EnsureLayout()
    {
        Directory.CreateDirectory(FolderPath);
        Directory.CreateDirectory(Path.Combine(FolderPath, AuditRepository.ComputersFolderName));
        Directory.CreateDirectory(CapturesFolder);
        Directory.CreateDirectory(ReportsFolder);
        Directory.CreateDirectory(PhotosFolder);
    }

    public void SaveManifest()
    {
        ThrowIfDisposed();
        _repository.WriteManifest(FolderPath, Manifest);
    }

    public IReadOnlyList<ComputerRecord> LoadComputers()
    {
        ThrowIfDisposed();
        return _repository.LoadComputers(FolderPath);
    }

    public ComputerRecord? TryLoadComputer(string computerId)
    {
        ThrowIfDisposed();
        return _repository.TryLoadComputer(FolderPath, computerId);
    }

    public void SaveComputer(ComputerRecord computer)
    {
        ThrowIfDisposed();
        _repository.SaveComputer(FolderPath, computer);
    }

    public CaptureRecord SaveCapture(
        ComputerRecord computer,
        InventorySnapshot snapshot,
        string? note = null,
        DeviceFingerprint? confirmedFingerprint = null)
    {
        ThrowIfDisposed();
        return _repository.SaveCapture(FolderPath, computer, snapshot, note, confirmedFingerprint);
    }

    public CaptureRecord LoadCapture(CaptureReference reference)
    {
        ThrowIfDisposed();
        return _repository.LoadCapture(FolderPath, reference.Id);
    }

    public AuditRecoveryReport Recover()
    {
        ThrowIfDisposed();
        return _repository.Recover(FolderPath);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _lock.Dispose();
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(AuditWorkspace));
        }
    }
}
