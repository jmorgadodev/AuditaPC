using AuditaPC.Core;
using AuditaPC.Core.Model;

namespace AuditaPC.App.ViewModels;

public sealed class ComputerListItem : ObservableObject
{
    public ComputerListItem(ComputerRecord record, CaptureRecord? currentCapture)
    {
        Record = record;
        CurrentCapture = currentCapture;
    }

    public ComputerRecord Record { get; }
    public CaptureRecord? CurrentCapture { get; private set; }

    public string Id => Record.Id;
    public string Name => string.IsNullOrWhiteSpace(Record.DisplayName) ? "Equipo sin nombre" : Record.DisplayName;

    public string ManufacturerModel
    {
        get
        {
            var identity = CurrentCapture?.Inventory.Identity.Data;
            var manufacturer = identity?.Manufacturer ?? Record.Fingerprint.Manufacturer;
            var model = identity?.Model ?? Record.Fingerprint.Model;
            var text = string.Join(" · ", new[] { manufacturer, model }
                .Where(value => !string.IsNullOrWhiteSpace(value)));
            return string.IsNullOrWhiteSpace(text) ? "Sin datos de modelo" : text;
        }
    }

    public string Serial =>
        CurrentCapture?.Inventory.Identity.Data?.BiosSerial
        ?? Record.Fingerprint.BiosSerial
        ?? "—";

    public string OperatingSystem =>
        CurrentCapture?.Inventory.OperatingSystem.Data?.FriendlyName
        ?? CurrentCapture?.Inventory.OperatingSystem.Data?.ProductName
        ?? "Sin captura automática";

    public string AssetCode =>
        string.IsNullOrWhiteSpace(Record.Manual.AssetCode) ? "—" : Record.Manual.AssetCode!;

    public string Location =>
        string.IsNullOrWhiteSpace(Record.Manual.Location) ? "—" : Record.Manual.Location!;

    public string LastCaptureText => Record.CurrentCapture is null
        ? "Sin capturas"
        : Formatting.Date(Record.CurrentCapture.CapturedAt);

    public string CapturesText => Record.Captures.Count == 1
        ? "1 captura"
        : $"{Record.Captures.Count} capturas";

    public int ProblemCount =>
        CurrentCapture?.Inventory.AllChecks().Count(check => check.Status == CheckStatus.Problem) ?? 0;

    public int IncompleteCount =>
        CurrentCapture?.Inventory.AllChecks().Count(check => check.Status.IsIncomplete()) ?? 0;

    public CheckStatus WorstStatus
    {
        get
        {
            if (CurrentCapture is null)
            {
                return CheckStatus.NotVerifiable;
            }

            var checks = CurrentCapture.Inventory.AllChecks().ToList();
            if (checks.Count == 0)
            {
                return CheckStatus.NotVerifiable;
            }

            return checks.OrderByDescending(check => check.Status.Severity()).First().Status;
        }
    }

    public string StatusText => WorstStatus.ToDisplayText();

    public bool HasCapture => CurrentCapture is not null;

    public void Update(ComputerRecord record, CaptureRecord? currentCapture)
    {
        CurrentCapture = currentCapture;
        RaisePropertyChanged(nameof(CurrentCapture));
        RaisePropertyChanged(nameof(Name));
        RaisePropertyChanged(nameof(ManufacturerModel));
        RaisePropertyChanged(nameof(Serial));
        RaisePropertyChanged(nameof(OperatingSystem));
        RaisePropertyChanged(nameof(AssetCode));
        RaisePropertyChanged(nameof(Location));
        RaisePropertyChanged(nameof(LastCaptureText));
        RaisePropertyChanged(nameof(CapturesText));
        RaisePropertyChanged(nameof(ProblemCount));
        RaisePropertyChanged(nameof(IncompleteCount));
        RaisePropertyChanged(nameof(WorstStatus));
        RaisePropertyChanged(nameof(StatusText));
        RaisePropertyChanged(nameof(HasCapture));
    }

    public void Refresh() => Update(Record, CurrentCapture);
}
