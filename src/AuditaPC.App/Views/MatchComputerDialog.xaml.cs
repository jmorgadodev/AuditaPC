using System.Windows;
using AuditaPC.App.ViewModels;
using AuditaPC.Core.Matching;
using AuditaPC.Core.Model;

namespace AuditaPC.App.Views;

public partial class MatchComputerDialog : Window
{
    private readonly MatchComputerViewModel _viewModel;

    public MatchComputerDialog(
        InventorySnapshot snapshot,
        DeviceMatchResult match,
        IReadOnlyList<ComputerRecord> records,
        string? technician)
    {
        InitializeComponent();
        _viewModel = new MatchComputerViewModel(snapshot, match, records, technician);
        DataContext = _viewModel;
    }

    public bool UpdateExisting => _viewModel.UpdateExisting;
    public ComputerRecord? SelectedRecord => _viewModel.SelectedRecord;
    public string DisplayName => _viewModel.DisplayName;
    public string AssetCode => _viewModel.AssetCode;
    public string CaptureNote => _viewModel.CaptureNote;

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!_viewModel.TryConfirm())
        {
            return;
        }

        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
