using System.Windows;
using System.Windows.Threading;
using AuditaPC.App.Services;
using AuditaPC.App.ViewModels;
using AuditaPC.Core.Storage;

namespace AuditaPC.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;

        try
        {
            var paths = AppPathResolver.Resolve(AppContext.BaseDirectory);
            var services = new AppServices(paths, paths.Settings);
            var viewModel = new MainViewModel(services);
            var window = new MainWindow(viewModel);
            MainWindow = window;
            window.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "AuditaPC no pudo iniciarse.\n\n" + ex.Message,
                "Error de inicio",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        DispatcherUnhandledException -= OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException -= OnDomainUnhandledException;
        base.OnExit(e);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(
            MainWindow,
            "Se produjo un error inesperado. La captura en curso no se guardará si no se completó.\n\n" +
            e.Exception.Message,
            "Error inesperado",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
    }

    private void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception)
        {
            MessageBox.Show(
                "Se produjo un error no controlado y la aplicación debe cerrarse.\n\n" +
                exception.Message,
                "Error no controlado",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }
}
