using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using AuditaPC.Core.Reporting;

namespace AuditaPC.App.Views;

public partial class ReportResultDialog : Window
{
    private readonly ReportBundle _bundle;

    public ReportResultDialog(ReportBundle bundle)
    {
        InitializeComponent();
        _bundle = bundle;

        var shortAuditId = string.IsNullOrWhiteSpace(bundle.AuditId)
            ? "—"
            : bundle.AuditId[..Math.Min(8, bundle.AuditId.Length)].ToUpperInvariant();
        Title = $"Informes generados — {bundle.CompanyName}";
        var computerCountText = bundle.ComputerCount == 1
            ? "1 equipo"
            : $"{bundle.ComputerCount} equipos";
        AuditContextText.Text =
            $"{bundle.CompanyName} · {bundle.AuditDate:dd-MM-yyyy} · " +
            $"{computerCountText} · Auditoría {shortAuditId}";
        GeneratedAtText.Text = $"Generado el {bundle.GeneratedAt:dd-MM-yyyy HH:mm:ss}";

        HtmlPathText.Text = bundle.HtmlPath;
        SummaryPathText.Text = bundle.SummaryCsvPath;
        SoftwarePathText.Text = bundle.SoftwareCsvPath;
        ChecksPathText.Text = bundle.ChecksCsvPath;
        JsonPathText.Text = bundle.JsonPath;

        foreach (var path in bundle.IndividualHtmlPaths)
        {
            IndividualReportsList.Items.Add(new ListBoxItem
            {
                Content = GetIndividualDisplayName(path),
                Tag = path
            });
        }

        if (bundle.IndividualHtmlPaths.Count > 0)
        {
            IndividualReportsPanel.Visibility = Visibility.Visible;
        }

        if (bundle.Warnings.Count > 0)
        {
            WarningsText.Text = string.Join(Environment.NewLine, bundle.Warnings);
            WarningsPanel.Visibility = Visibility.Visible;
        }
    }

    private static string GetIndividualDisplayName(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        const string marker = "-equipo-";
        var markerIndex = name.LastIndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (markerIndex >= 0)
        {
            name = name[(markerIndex + marker.Length)..];
        }

        var idSeparator = name.LastIndexOf('-');
        var shortComputerId = string.Empty;
        if (idSeparator >= 0 && name.Length - idSeparator - 1 >= 8)
        {
            var computerId = name[(idSeparator + 1)..];
            shortComputerId = computerId[..Math.Min(6, computerId.Length)].ToUpperInvariant();
            name = name[..idSeparator];
        }

        var displayName = name.Replace('-', ' ');
        return string.IsNullOrWhiteSpace(shortComputerId)
            ? displayName
            : $"{displayName} · {shortComputerId}";
    }

    private void IndividualReportsList_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        OpenIndividualButton.IsEnabled =
            IndividualReportsList.SelectedItem is ListBoxItem { Tag: string };
    }

    private void OpenHtml_Click(object sender, RoutedEventArgs e) => Open(_bundle.HtmlPath);

    private void OpenIndividual_Click(object sender, RoutedEventArgs e)
    {
        if (IndividualReportsList.SelectedItem is ListBoxItem { Tag: string path })
        {
            Open(path);
        }
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        Open(Path.GetDirectoryName(_bundle.HtmlPath) ?? _bundle.HtmlPath);
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void Open(string path)
    {
        try
        {
            if (!File.Exists(path) && !Directory.Exists(path))
            {
                MessageBox.Show(
                    this,
                    $"No se encontró el archivo o carpeta esperado:\n{path}\n\n" +
                    "Vuelve a generar los informes desde la auditoría correcta.",
                    "Informe no disponible",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                ex.Message,
                "No se pudo abrir el archivo",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }
}
