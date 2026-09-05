using System.ComponentModel;
using System.IO;
using System.Windows;
using AiAgileBoard.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.Web.WebView2.Core;

namespace AiAgileBoard.Desktop;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable", Justification = "WPF owns the window lifetime; Closing awaits startup and disposes all resources before allowing the window to close.")]
public partial class MainWindow : Window
{
    private readonly CancellationTokenSource _shutdown = new();
    private WebApplication? _host;
    private Task _startup = Task.CompletedTask;
    private bool _closing;
    private bool _canClose;
    private Uri? _origin;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => _startup = StartAsync();
        Closing += CloseAsync;
    }

    internal void ActivateWindow()
    {
        if (_closing) return;
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Show();
        Activate();
    }

    private async Task StartAsync()
    {
        try
        {
            var directory = AppContext.BaseDirectory;
            var runtime = Path.Combine(directory, "WebView2Runtime");
            if (!File.Exists(Path.Combine(runtime, "msedgewebview2.exe")) ||
                !File.Exists(Path.Combine(directory, "wwwroot", "index.html")))
            {
                throw new InvalidOperationException("Application files are missing. Extract the entire ZIP into a writable folder, then run AiAgileBoard.exe from that folder.");
            }
            var profile = DesktopStorage.PrepareBrowserProfile(directory);
            await Task.Run(async () =>
            {
                _host = DesktopHost.Build(directory);
                await BoardHost.InitializeDatabaseAsync(_host, _shutdown.Token);
                await _host.StartAsync(_shutdown.Token);
                _origin = DesktopHost.Address(_host);
            });
            _shutdown.Token.ThrowIfCancellationRequested();
            Status.Text = "Opening your board…";
            var environment = await CoreWebView2Environment.CreateAsync(runtime, profile);
            _shutdown.Token.ThrowIfCancellationRequested();
            await Browser.EnsureCoreWebView2Async(environment);
            _shutdown.Token.ThrowIfCancellationRequested();
            var core = Browser.CoreWebView2;
            core.Settings.AreDevToolsEnabled = false;
            core.Settings.AreHostObjectsAllowed = false;
            core.Settings.IsWebMessageEnabled = false;
            core.Settings.AreDefaultContextMenusEnabled = false;
            core.NavigationStarting += (_, e) => e.Cancel = !IsApplicationUri(e.Uri);
            core.FrameNavigationStarting += (_, e) => e.Cancel = !IsApplicationUri(e.Uri);
            core.NewWindowRequested += (_, e) => e.Handled = true;
            core.PermissionRequested += (_, e) => e.State = CoreWebView2PermissionState.Deny;
            core.DownloadStarting += (_, e) => e.Cancel = true;
            core.ProcessFailed += (_, _) => ShowError("The embedded browser stopped. Close and reopen AI Agile Board.");
            core.NavigationCompleted += (_, e) =>
            {
                if (e.IsSuccess)
                {
                    Status.Visibility = Visibility.Collapsed;
                    Browser.Visibility = Visibility.Visible;
                }
                else if (!_closing && e.WebErrorStatus != CoreWebView2WebErrorStatus.OperationCanceled)
                {
                    ShowError("The board could not load. Close and reopen AI Agile Board. Error: " + e.WebErrorStatus);
                }
            };
            core.Navigate(_origin!.AbsoluteUri);
        }
        catch (OperationCanceledException) when (_shutdown.IsCancellationRequested) { }
        catch (Exception exception)
        {
            ShowError("AI Agile Board could not start. Extract the complete ZIP to a folder you can write to. " +
                "Check the configured database path and available disk space.\n\n" + exception.Message);
            await StopHostAsync();
        }
    }

    private bool IsApplicationUri(string value) => Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && _origin is not null && uri.Scheme == _origin.Scheme && uri.Host == _origin.Host
        && uri.Port == _origin.Port && string.IsNullOrEmpty(uri.UserInfo);

    private void ShowError(string message)
    {
        Browser.Visibility = Visibility.Hidden;
        Status.Text = message;
        Status.Visibility = Visibility.Visible;
    }

    private async void CloseAsync(object? sender, CancelEventArgs e)
    {
        if (_canClose) return;
        e.Cancel = true;
        if (_closing) return;
        _closing = true;
        _shutdown.Cancel();
        try
        {
            await _startup;
            Browser.Dispose();
            await StopHostAsync();
        }
        finally
        {
            _shutdown.Dispose();
            _canClose = true;
            // Cleanup can complete synchronously, while WPF is still dispatching Closing.
            _ = Dispatcher.BeginInvoke(Close);
        }
    }

    private async Task StopHostAsync()
    {
        if (_host is null) return;
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await _host.StopAsync(timeout.Token);
        }
        catch (OperationCanceledException) { }
        finally
        {
            await _host.DisposeAsync();
            _host = null;
        }
    }
}
