using System.Windows;
using AuditaPC.Core.Model;

namespace AuditaPC.App.Views;

public partial class FieldNotesDialog : Window
{
    public FieldNotesDialog(AuditManifest manifest)
    {
        InitializeComponent();
        VisitSummaryBox.Text = manifest.VisitSummary ?? string.Empty;
        ConnectivityBox.Text = manifest.ConnectivityNotes ?? string.Empty;
        ModulesBox.Text = manifest.ModulesNotes ?? string.Empty;
        ConditionBox.Text = manifest.ConditionNotes ?? string.Empty;
        FindingsBox.Text = manifest.FindingsNotes ?? string.Empty;
        ActionPlanBox.Text = manifest.ActionPlanNotes ?? string.Empty;
        LimitationsBox.Text = manifest.LimitationsNotes ?? string.Empty;
        OtherBox.Text = manifest.OtherNotes ?? string.Empty;
    }

    public void ApplyTo(AuditManifest manifest)
    {
        manifest.VisitSummary = Normalize(VisitSummaryBox.Text);
        manifest.ConnectivityNotes = Normalize(ConnectivityBox.Text);
        manifest.ModulesNotes = Normalize(ModulesBox.Text);
        manifest.ConditionNotes = Normalize(ConditionBox.Text);
        manifest.FindingsNotes = Normalize(FindingsBox.Text);
        manifest.ActionPlanNotes = Normalize(ActionPlanBox.Text);
        manifest.LimitationsNotes = Normalize(LimitationsBox.Text);
        manifest.OtherNotes = Normalize(OtherBox.Text);
        manifest.UpdatedAt = DateTimeOffset.Now;
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
