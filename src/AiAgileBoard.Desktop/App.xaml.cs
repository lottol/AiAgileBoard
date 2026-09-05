using System.Windows;

namespace AiAgileBoard.Desktop;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable", Justification = "WPF owns the application lifetime; OnExit disposes the instance coordinator on the UI thread.")]
public partial class App : System.Windows.Application
{
    private SingleInstance? _instance;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _instance = new SingleInstance();
        if (!_instance.IsPrimary)
        {
            _instance.ActivatePrimary();
            Shutdown();
            return;
        }

        var window = new MainWindow();
        MainWindow = window;
        _instance.Listen(() => Dispatcher.BeginInvoke(window.ActivateWindow));
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _instance?.Dispose();
        base.OnExit(e);
    }
}
