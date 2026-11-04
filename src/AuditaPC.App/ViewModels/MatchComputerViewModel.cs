using System.Collections.ObjectModel;
using AuditaPC.Core;
using AuditaPC.Core.Matching;
using AuditaPC.Core.Model;

namespace AuditaPC.App.ViewModels;

public sealed class MatchCandidateRow
{
    public MatchCandidateRow(DeviceMatchCandidate candidate)
    {
        Candidate = candidate;
    }

    public DeviceMatchCandidate Candidate { get; }
    public ComputerRecord Record => Candidate.Record;
    public string Name => Candidate.CurrentName ?? Record.DisplayName;
    public string ScoreText => $"{Candidate.Score} pts";
    public string ReasonsText => Candidate.Reasons.Count == 0
        ? "Sin coincidencias fuertes"
        : string.Join(" · ", Candidate.Reasons);
    public string ConflictsText => Candidate.Conflicts.Count == 0
        ? string.Empty
        : string.Join(" · ", Candidate.Conflicts);
    public bool HasConflicts => Candidate.Conflicts.Count > 0;
    public string LastCaptureText => Candidate.LastCaptureAt is null
        ? "Sin capturas"
        : "Última captura: " + Formatting.Date(Candidate.LastCaptureAt);
    public string Serial => string.IsNullOrWhiteSpace(Record.Fingerprint.BiosSerial)
        ? "Serie no registrada"
        : "Serie " + Record.Fingerprint.BiosSerial;
    public string AssetCode => string.IsNullOrWhiteSpace(Record.Manual.AssetCode)
        ? "Sin código de activo"
        : "Activo " + Record.Manual.AssetCode;
}

public sealed class MatchComputerViewModel : ObservableObject
{
    private readonly InventorySnapshot _snapshot;
    private MatchCandidateRow? _selectedCandidate;
    private bool _updateExisting;
    private bool _createNew = true;
    private string _displayName;
    private string _assetCode = string.Empty;
    private string _captureNote = string.Empty;
    private string _errorMessage = string.Empty;

    public MatchComputerViewModel(
        InventorySnapshot snapshot,
        DeviceMatchResult match,
        IReadOnlyList<ComputerRecord> records,
        string? technician)
    {
        _snapshot = snapshot;
        Match = match;
        Technician = technician;

        foreach (var candidate in match.Candidates)
        {
            Candidates.Add(new MatchCandidateRow(candidate));
        }

        _displayName = FingerprintFactory.GetDisplayName(snapshot);
        SnapshotName = _displayName;
        SnapshotModel = Join(
            snapshot.Identity.Data?.Manufacturer,
            snapshot.Identity.Data?.Model);
        SnapshotOs = snapshot.OperatingSystem.Data?.FriendlyName
                     ?? snapshot.OperatingSystem.Data?.ProductName
                     ?? "Sistema operativo no determinado";
        SnapshotSerial = snapshot.Identity.Data?.BiosSerial ?? "Sin serie";
        ProblemCount = snapshot.AllChecks().Count(check => check.Status == CheckStatus.Problem);
        IncompleteCount = snapshot.AllChecks().Count(check => check.Status.IsIncomplete());

        if (match.Confidence == MatchConfidence.Confident && match.Best is not null)
        {
            _updateExisting = true;
            _createNew = false;
            SelectedCandidate = Candidates.FirstOrDefault(row =>
                ReferenceEquals(row.Candidate, match.Best))
                ?? Candidates.FirstOrDefault();
        }
        else
        {
            _updateExisting = false;
            _createNew = true;
        }

        if (SelectedCandidate is not null
            && !string.IsNullOrWhiteSpace(SelectedCandidate.Record.Manual.AssetCode))
        {
            _assetCode = SelectedCandidate.Record.Manual.AssetCode!;
        }
    }

    public DeviceMatchResult Match { get; }
    public string? Technician { get; }
    public ObservableCollection<MatchCandidateRow> Candidates { get; } = new();

    public string Explanation => Match.Explanation;
    public bool HasCandidates => Candidates.Count > 0;
    public string SnapshotName { get; }
    public string SnapshotModel { get; }
    public string SnapshotOs { get; }
    public string SnapshotSerial { get; }
    public int ProblemCount { get; }
    public int IncompleteCount { get; }
    public string ConfidenceText => Match.Confidence switch
    {
        MatchConfidence.Confident => "Coincidencia confiable",
        MatchConfidence.Possible => "Coincidencia parcial",
        MatchConfidence.Ambiguous => "Coincidencia ambigua",
        _ => "Sin coincidencia"
    };

    public MatchCandidateRow? SelectedCandidate
    {
        get => _selectedCandidate;
        set
        {
            if (!Set(ref _selectedCandidate, value))
            {
                return;
            }

            if (value is not null && UpdateExisting)
            {
                if (!string.IsNullOrWhiteSpace(value.Record.Manual.AssetCode))
                {
                    AssetCode = value.Record.Manual.AssetCode!;
                }
            }
        }
    }

    public bool UpdateExisting
    {
        get => _updateExisting;
        set
        {
            if (!Set(ref _updateExisting, value))
            {
                return;
            }

            if (value)
            {
                CreateNew = false;
            }

            RaisePropertyChanged(nameof(ModeHint));
        }
    }

    public bool CreateNew
    {
        get => _createNew;
        set
        {
            if (!Set(ref _createNew, value))
            {
                return;
            }

            if (value)
            {
                UpdateExisting = false;
            }

            RaisePropertyChanged(nameof(ModeHint));
        }
    }

    public string ModeHint => UpdateExisting
        ? "Se agregará una nueva captura al equipo seleccionado. Las capturas anteriores se conservan."
        : "Se creará una ficha nueva. No se fusionará automáticamente con ningún equipo existente.";

    public string DisplayName
    {
        get => _displayName;
        set => Set(ref _displayName, value);
    }

    public string AssetCode
    {
        get => _assetCode;
        set => Set(ref _assetCode, value);
    }

    public string CaptureNote
    {
        get => _captureNote;
        set => Set(ref _captureNote, value);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (Set(ref _errorMessage, value))
            {
                RaisePropertyChanged(nameof(HasError));
            }
        }
    }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public ComputerRecord? SelectedRecord => SelectedCandidate?.Record;

    public bool TryConfirm()
    {
        ErrorMessage = string.Empty;

        if (UpdateExisting && SelectedCandidate is null)
        {
            ErrorMessage = "Selecciona el equipo que deseas actualizar o elige crear un equipo nuevo.";
            return false;
        }

        if (CreateNew && string.IsNullOrWhiteSpace(DisplayName))
        {
            DisplayName = SnapshotName;
        }

        return true;
    }

    private static string Join(params string?[] values)
    {
        var text = string.Join(" · ", values.Where(value => !string.IsNullOrWhiteSpace(value)));
        return string.IsNullOrWhiteSpace(text) ? "Modelo no determinado" : text;
    }
}
