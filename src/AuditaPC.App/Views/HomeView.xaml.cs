using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AuditaPC.App.ViewModels;
using AuditaPC.Core.Model;

namespace AuditaPC.App.Views;

public partial class HomeView : UserControl
{
    private bool? _isCompact;

    public HomeView()
    {
        InitializeComponent();
        SizeChanged += (_, _) => ApplyResponsiveLayout();
        Loaded += (_, _) => ApplyResponsiveLayout();
    }

    private HomeViewModel? ViewModel => DataContext as HomeViewModel;

    private void ApplyResponsiveLayout()
    {
        if (ActualWidth <= 0)
        {
            return;
        }

        var compact = ActualWidth < 980;
        if (_isCompact == compact)
        {
            return;
        }

        _isCompact = compact;

        if (compact)
        {
            HomeHeader.RowDefinitions[1].Height = GridLength.Auto;
            Grid.SetRow(HomeHeaderActions, 1);
            Grid.SetColumn(HomeHeaderActions, 0);
            Grid.SetColumnSpan(HomeHeaderActions, 2);
            HomeHeaderActions.HorizontalAlignment = HorizontalAlignment.Left;
            HomeHeaderActions.Margin = new Thickness(0, 12, 0, 0);
            Grid.SetColumnSpan(HomeHeaderTitle, 2);
        }
        else
        {
            HomeHeader.RowDefinitions[1].Height = new GridLength(0);
            Grid.SetRow(HomeHeaderActions, 0);
            Grid.SetColumn(HomeHeaderActions, 1);
            Grid.SetColumnSpan(HomeHeaderActions, 1);
            HomeHeaderActions.HorizontalAlignment = HorizontalAlignment.Right;
            HomeHeaderActions.Margin = new Thickness(20, 0, 0, 0);
            Grid.SetColumnSpan(HomeHeaderTitle, 1);
        }
    }

    private void NewAudit_Click(object sender, RoutedEventArgs e) => ViewModel?.CreateRequested();

    private void OpenAudit_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is AuditCatalogItem item)
        {
            ViewModel?.OpenRequested(item.FolderPath);
        }
    }

    private void AuditCard_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is AuditCatalogItem item)
        {
            ViewModel?.OpenRequested(item.FolderPath);
        }
    }
}
