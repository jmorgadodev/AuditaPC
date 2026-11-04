using System.Windows;
using System.Windows.Controls;

namespace AuditaPC.App.Views;

public partial class WorkspaceView : UserControl
{
    private bool? _isCompact;

    public WorkspaceView()
    {
        InitializeComponent();
        SizeChanged += (_, _) => ApplyResponsiveLayout();
        Loaded += (_, _) => ApplyResponsiveLayout();
    }

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
            WorkspaceHeader.RowDefinitions[1].Height = GridLength.Auto;
            Grid.SetRow(WorkspaceHeaderActions, 1);
            Grid.SetColumn(WorkspaceHeaderActions, 0);
            Grid.SetColumnSpan(WorkspaceHeaderActions, 2);
            WorkspaceHeaderActions.HorizontalAlignment = HorizontalAlignment.Left;
            Grid.SetColumnSpan(WorkspaceHeaderTitle, 2);

            WorkspaceBody.RowDefinitions[0].Height = new GridLength(260);
            WorkspaceBody.RowDefinitions[1].Height = new GridLength(1, GridUnitType.Star);
            WorkspaceBody.ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);
            WorkspaceBody.ColumnDefinitions[1].Width = new GridLength(0);
            WorkspaceBody.ColumnDefinitions[2].Width = new GridLength(0);

            Grid.SetRow(ComputerListPanel, 0);
            Grid.SetColumn(ComputerListPanel, 0);
            Grid.SetColumnSpan(ComputerListPanel, 3);
            Grid.SetRow(ComputerDetailPanel, 1);
            Grid.SetColumn(ComputerDetailPanel, 0);
            Grid.SetColumnSpan(ComputerDetailPanel, 3);
        }
        else
        {
            WorkspaceHeader.RowDefinitions[1].Height = new GridLength(0);
            Grid.SetRow(WorkspaceHeaderActions, 0);
            Grid.SetColumn(WorkspaceHeaderActions, 1);
            Grid.SetColumnSpan(WorkspaceHeaderActions, 1);
            WorkspaceHeaderActions.HorizontalAlignment = HorizontalAlignment.Right;
            Grid.SetColumnSpan(WorkspaceHeaderTitle, 1);

            WorkspaceBody.RowDefinitions[0].Height = new GridLength(1, GridUnitType.Star);
            WorkspaceBody.RowDefinitions[1].Height = new GridLength(0);
            WorkspaceBody.ColumnDefinitions[0].Width = new GridLength(2, GridUnitType.Star);
            WorkspaceBody.ColumnDefinitions[1].Width = new GridLength(22);
            WorkspaceBody.ColumnDefinitions[2].Width = new GridLength(3, GridUnitType.Star);

            Grid.SetRow(ComputerListPanel, 0);
            Grid.SetColumn(ComputerListPanel, 0);
            Grid.SetColumnSpan(ComputerListPanel, 1);
            Grid.SetRow(ComputerDetailPanel, 0);
            Grid.SetColumn(ComputerDetailPanel, 2);
            Grid.SetColumnSpan(ComputerDetailPanel, 1);
        }
    }
}
