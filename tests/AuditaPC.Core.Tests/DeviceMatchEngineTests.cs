using AuditaPC.Core.Matching;
using AuditaPC.Core.Model;

namespace AuditaPC.Core.Tests;

public sealed class DeviceMatchEngineTests
{
    private readonly DeviceMatchEngine _engine = new();

    [Fact]
    public void Identical_uuid_and_serial_are_confident()
    {
        var incoming = new DeviceFingerprint
        {
            ComputerName = "EQUIPO-01",
            Manufacturer = "Fabricante",
            Model = "Modelo",
            BiosSerial = "SERIE-REAL-01",
            SystemUuid = "11111111-2222-3333-4444-555555555555",
            MachineGuid = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"
        };
        var record = Record(incoming);

        var result = _engine.FindMatches(incoming, new[] { record });

        Assert.Equal(MatchConfidence.Confident, result.Confidence);
        Assert.Same(record, result.Best!.Record);
        Assert.True(result.Best.Score >= DeviceMatchEngine.ConfidentThreshold);
    }

    [Fact]
    public void Generic_serial_values_are_ignored()
    {
        var incoming = new DeviceFingerprint
        {
            ComputerName = "EQUIPO-01",
            BiosSerial = "To be filled by O.E.M.",
            SystemUuid = "FFFFFFFF-FFFF-FFFF-FFFF-FFFFFFFFFFFF",
            BaseboardSerial = "000000000000000",
            DiskSerials = { "0000000000" }
        };
        var record = Record(new DeviceFingerprint
        {
            ComputerName = "EQUIPO-02",
            BiosSerial = "To be filled by O.E.M.",
            SystemUuid = "FFFFFFFF-FFFF-FFFF-FFFF-FFFFFFFFFFFF",
            BaseboardSerial = "000000000000000",
            DiskSerials = { "0000000000" }
        });

        var result = _engine.FindMatches(incoming, new[] { record });

        Assert.Equal(MatchConfidence.None, result.Confidence);
        Assert.Empty(result.Candidates);
    }

    [Fact]
    public void Different_asset_code_produces_possible_match_and_conflict()
    {
        var incoming = new DeviceFingerprint
        {
            AssetCode = "ACT-002",
            BiosSerial = "SERIE-REAL-02",
            SystemUuid = "22222222-3333-4444-5555-666666666666"
        };
        var record = Record(new DeviceFingerprint
        {
            AssetCode = "ACT-001",
            BiosSerial = "SERIE-REAL-02",
            SystemUuid = "22222222-3333-4444-5555-666666666666"
        });

        var result = _engine.FindMatches(incoming, new[] { record });

        Assert.Equal(MatchConfidence.Possible, result.Confidence);
        Assert.True(result.Best!.AssetCodeConflict);
        Assert.Contains("Código de activo distinto", result.Best.Conflicts);
    }

    [Fact]
    public void Two_identical_records_are_ambiguous()
    {
        var fingerprint = new DeviceFingerprint
        {
            BiosSerial = "SERIE-REAL-03",
            SystemUuid = "33333333-4444-5555-6666-777777777777"
        };
        var first = Record(fingerprint);
        var second = Record(fingerprint);

        var result = _engine.FindMatches(fingerprint, new[] { first, second });

        Assert.Equal(MatchConfidence.Ambiguous, result.Confidence);
        Assert.Equal(2, result.Candidates.Count);
    }

    [Fact]
    public void Engine_never_merges_records_automatically()
    {
        var fingerprint = new DeviceFingerprint
        {
            BiosSerial = "SERIE-REAL-04",
            SystemUuid = "44444444-5555-6666-7777-888888888888"
        };
        var record = Record(fingerprint);
        var originalId = record.Id;

        var result = _engine.FindMatches(fingerprint, new[] { record });

        Assert.Equal(MatchConfidence.Confident, result.Confidence);
        Assert.Equal(originalId, result.Best!.Record.Id);
        Assert.Single(result.Candidates);
    }

    private static ComputerRecord Record(DeviceFingerprint fingerprint) => new()
    {
        DisplayName = fingerprint.ComputerName ?? "Equipo de prueba",
        Fingerprint = fingerprint
    };
}
