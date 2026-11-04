using System.ComponentModel;
using System.Windows;
using AuditaPC.App.ViewModels;

namespace AuditaPC.App;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();

        var workArea = SystemParameters.WorkArea;
        Width = Math.Min(Width, Math.Max(MinWidth, workArea.Width - 24));
        Height = Math.Min(Height, Math.Max(MinHeight, workArea.Height - 24));

        DataContext = viewModel;
        Loaded += (_, _) => ApplyResponsiveLayout();
        Closing += OnClosing;
    }

    private MainViewModel? ViewModel => DataContext as MainViewModel;

    private void OnWindowSizeChanged(object sender, SizeChangedEventArgs e) =>
        ApplyResponsiveLayout();

    private void ApplyResponsiveLayout()
    {
        if (ActualWidth < 1160 && ViewModel?.IsSidebarExpanded == true)
        {
            ViewModel.IsSidebarExpanded = false;
        }
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (ViewModel is not null && !ViewModel.TryCloseForShutdown())
        {
            e.Cancel = true;
        }
    }
}
