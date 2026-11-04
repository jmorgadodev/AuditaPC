using AuditaPC.Core.Model;

namespace AuditaPC.Core.Matching;

public sealed class DeviceMatchCandidate
{
    public ComputerRecord Record { get; init; } = new();
    public int Score { get; init; }
    public List<string> Reasons { get; init; } = new();
    public List<string> Conflicts { get; init; } = new();
    public bool AssetCodeConflict { get; init; }
    public string? CurrentName { get; init; }
    public DateTimeOffset? LastCaptureAt { get; init; }
}

public enum MatchConfidence
{
    None,
    Possible,
    Confident,
    Ambiguous
}

public sealed class DeviceMatchResult
{
    public MatchConfidence Confidence { get; init; }
    public List<DeviceMatchCandidate> Candidates { get; init; } = new();
    public DeviceMatchCandidate? Best => Candidates.FirstOrDefault();
    public string Explanation { get; init; } = string.Empty;
}

/// <summary>
/// Compara huellas con puntuación conservadora. La coincidencia "confiable"
/// solo se usa para preseleccionar un equipo: nunca fusiona registros sin que
/// el técnico confirme la operación.
/// </summary>
public sealed class DeviceMatchEngine
{
    public const int ConfidentThreshold = 60;
    public const int PossibleThreshold = 25;
    public const int AmbiguityMargin = 15;

    public DeviceMatchResult FindMatches(DeviceFingerprint fingerprint, IEnumerable<ComputerRecord> records)
    {
        var candidates = new List<DeviceMatchCandidate>();

        foreach (var record in records)
        {
            var candidate = Score(fingerprint, record);
            if (candidate is not null)
            {
                candidates.Add(candidate);
            }
        }

        candidates = candidates
            .OrderByDescending(c => c.Score)
            .ThenBy(c => c.CurrentName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        if (candidates.Count == 0)
        {
            return new DeviceMatchResult
            {
                Confidence = MatchConfidence.None,
                Explanation = "No se encontraron coincidencias con los equipos de esta auditoría."
            };
        }

        var best = candidates[0];
        var second = candidates.Count > 1 ? candidates[1] : null;
        var margin = second is null ? best.Score : best.Score - second.Score;

        if (best.AssetCodeConflict)
        {
            return new DeviceMatchResult
            {
                Confidence = MatchConfidence.Possible,
                Candidates = candidates,
                Explanation = "Hay datos de hardware parecidos, pero el código de activo es distinto. " +
                              "Revisa la ficha antes de actualizar."
            };
        }

        if (best.Score >= ConfidentThreshold && margin >= AmbiguityMargin)
        {
            return new DeviceMatchResult
            {
                Confidence = MatchConfidence.Confident,
                Candidates = candidates,
                Explanation = "Coincidencia confiable. Confirma si deseas actualizar este equipo o crear uno nuevo."
            };
        }

        if (best.Score >= ConfidentThreshold)
        {
            return new DeviceMatchResult
            {
                Confidence = MatchConfidence.Ambiguous,
                Candidates = candidates,
                Explanation = "Hay varios equipos con datos parecidos. Selecciona el correcto o crea un equipo nuevo."
            };
        }

        if (best.Score >= PossibleThreshold)
        {
            return new DeviceMatchResult
            {
                Confidence = MatchConfidence.Possible,
                Candidates = candidates,
                Explanation = "Coincidencia parcial. Verifica la ficha antes de actualizar."
            };
        }

        return new DeviceMatchResult
        {
            Confidence = MatchConfidence.None,
            Candidates = candidates,
            Explanation = "No hay coincidencias suficientemente fiables."
        };
    }

    private static DeviceMatchCandidate? Score(DeviceFingerprint incoming, ComputerRecord record)
    {
        var stored = record.Fingerprint;
        var score = 0;
        var reasons = new List<string>();
        var conflicts = new List<string>();

        if (MatchNormalization.Equal(incoming.AssetCode, stored.AssetCode))
        {
            score += 65;
            reasons.Add("Código de activo idéntico");
        }
        else if (MatchNormalization.IsUsable(incoming.AssetCode)
                 && MatchNormalization.IsUsable(stored.AssetCode)
                 && !MatchNormalization.Equal(incoming.AssetCode, stored.AssetCode))
        {
            conflicts.Add("Código de activo distinto");
        }

        if (MatchNormalization.Equal(incoming.SystemUuid, stored.SystemUuid))
        {
            score += 70;
            reasons.Add("UUID SMBIOS idéntico");
        }

        if (MatchNormalization.Equal(incoming.MachineGuid, stored.MachineGuid))
        {
            score += 55;
            reasons.Add("GUID de instalación de Windows idéntico");
        }

        if (MatchNormalization.Equal(incoming.BiosSerial, stored.BiosSerial))
        {
            score += 40;
            reasons.Add("Número de serie del BIOS idéntico");
        }

        if (MatchNormalization.Equal(incoming.BaseboardSerial, stored.BaseboardSerial))
        {
            score += 30;
            reasons.Add("Serie de placa base idéntica");
        }

        var incomingDisks = incoming.DiskSerials.Where(MatchNormalization.IsUsable)
            .Select(MatchNormalization.Normalize).Where(s => s.Length > 0).ToHashSet();
        var storedDisks = stored.DiskSerials.Where(MatchNormalization.IsUsable)
            .Select(MatchNormalization.Normalize).Where(s => s.Length > 0).ToHashSet();
        if (incomingDisks.Overlaps(storedDisks))
        {
            score += 25;
            reasons.Add("Número de serie de disco idéntico");
        }

        if (MatchNormalization.Equal(incoming.BiosAssetTag, stored.BiosAssetTag))
        {
            score += 25;
            reasons.Add("Código de activo del BIOS idéntico");
        }

        if (MatchNormalization.EqualLoose(incoming.Manufacturer, stored.Manufacturer)
            && MatchNormalization.EqualLoose(incoming.Model, stored.Model)
            && MatchNormalization.IsUsable(incoming.Model))
        {
            score += 15;
            reasons.Add("Fabricante y modelo coinciden");
        }

        if (MatchNormalization.Equal(incoming.ComputerName, stored.ComputerName))
        {
            score += 10;
            reasons.Add("Nombre del equipo coincide");
        }

        if (MatchNormalization.EqualLoose(incoming.Domain, stored.Domain))
        {
            score += 2;
        }

        if (score <= 0)
        {
            return null;
        }

        return new DeviceMatchCandidate
        {
            Record = record,
            Score = score,
            Reasons = reasons,
            Conflicts = conflicts,
            AssetCodeConflict = conflicts.Count > 0,
            CurrentName = record.DisplayName,
            LastCaptureAt = record.CurrentCapture?.CapturedAt
        };
    }
}
