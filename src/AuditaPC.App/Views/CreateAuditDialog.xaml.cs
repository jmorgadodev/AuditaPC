using System.Windows;

namespace AuditaPC.App.Views;

public partial class CreateAuditDialog : Window
{
    public CreateAuditDialog(string? defaultTechnician = null)
    {
        InitializeComponent();
        TechnicianBox.Text = defaultTechnician ?? string.Empty;
        StartDatePicker.SelectedDate = DateTime.Today;
        Loaded += (_, _) => CompanyBox.Focus();
    }

    public string CompanyName { get; private set; } = string.Empty;
    public string? Technician { get; private set; }
    public DateTime StartDate { get; private set; } = DateTime.Today;
    public string? Notes { get; private set; }

    private void Create_Click(object sender, RoutedEventArgs e)
    {
        var company = CompanyBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(company))
        {
            ShowError("Ingresa el nombre de la empresa o cliente.");
            CompanyBox.Focus();
            return;
        }

        CompanyName = company;
        Technician = string.IsNullOrWhiteSpace(TechnicianBox.Text) ? null : TechnicianBox.Text.Trim();
        StartDate = StartDatePicker.SelectedDate ?? DateTime.Today;
        Notes = string.IsNullOrWhiteSpace(NotesBox.Text) ? null : NotesBox.Text.Trim();
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }
}
