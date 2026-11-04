using System.ComponentModel;
using System.Windows;
using AuditaPC.App.ViewModels;

namespace AuditaPC.App.Views;

public partial class CaptureProgressWindow : Window
{
    private bool _started;

    public CaptureProgressWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Closing += OnClosing;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_started || DataContext is not CaptureProgressViewModel viewModel)
        {
            return;
        }

        _started = true;
        await viewModel.StartAsync();
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (DataContext is CaptureProgressViewModel viewModel && viewModel.IsRunning)
        {
            viewModel.Cancel();
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is CaptureProgressViewModel viewModel)
        {
            viewModel.Cancel();
        }
    }

    private void Continue_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }
}
