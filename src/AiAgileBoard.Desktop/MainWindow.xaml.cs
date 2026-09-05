using System.ComponentModel;
using System.IO;
using System.Windows;
using AiAgileBoard.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.Web.WebView2.Core;
using AiAgileBoard.Data.Projects;
using Microsoft.Win32;
using System.Text.Json;

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
    private readonly SemaphoreSlim _operations = new(1, 1);
    private ProjectSession? _session;
    private string? _projectError;
    private static readonly JsonSerializerOptions BridgeJson = new(JsonSerializerDefaults.Web);
    private static string RecoveryRoot => Path.Combine(AppContext.BaseDirectory, "recovery");

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
                await _host.StartAsync(_shutdown.Token);
                _origin = DesktopHost.Address(_host);
            });
            _shutdown.Token.ThrowIfCancellationRequested();
            Status.Text = "Opening AI Agile Board…";
            var environment = await CoreWebView2Environment.CreateAsync(runtime, profile);
            _shutdown.Token.ThrowIfCancellationRequested();
            await Browser.EnsureCoreWebView2Async(environment);
            _shutdown.Token.ThrowIfCancellationRequested();
            var core = Browser.CoreWebView2;
            core.Settings.AreDevToolsEnabled = false;
            core.Settings.AreHostObjectsAllowed = false;
            core.Settings.IsWebMessageEnabled = true;
            await core.AddScriptToExecuteOnDocumentCreatedAsync("window.__aiabDesktop = true;");
            core.WebMessageReceived += ReceiveProjectCommand;
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
                "Check the application folder permissions and available disk space.\n\n" + exception.Message);
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

    private void PublishState()
    {
        if (_closing || _canClose || Browser.CoreWebView2 is null || !IsApplicationUri(Browser.Source?.AbsoluteUri ?? "")) return;
        Browser.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new
        {
            type = "projectState",
            projectName = _session is null ? null : Path.GetFileName(_session.ArchivePath),
            saveStatus = _session?.SaveStatus,
            error = _projectError ?? _session?.SaveError,
            settings = _session?.Settings,
            recoveryAvailable = _session is null && ProjectSession.FindRecoveryDirectories(RecoveryRoot).Count > 0
        }, BridgeJson));
    }

    private void SessionChanged(object? sender, EventArgs e) =>
        _ = Dispatcher.BeginInvoke(PublishState);

    private async void ReceiveProjectCommand(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        var source = e.Source;
        var json = e.WebMessageAsJson;
        if (_closing || !IsApplicationUri(source) || json.Length > 1024 * 1024 + 1024) return;
        // Native modal dialogs must run after WebView2's event callback has unwound.
        await Task.Yield();
        if (_closing || !IsApplicationUri(source)) return;
        if (!await _operations.WaitAsync(0)) return;
        try
        {
            using var message = JsonDocument.Parse(json);
            if (message.RootElement.ValueKind != JsonValueKind.Object ||
                !message.RootElement.TryGetProperty("command", out var command) || command.ValueKind != JsonValueKind.String) return;
            if (command.GetString() != "getState") _projectError = null;
            switch (command.GetString())
            {
                case "getState": break;
                case "newProject":
                case "openProject":
                    if (_session is not null) throw new InvalidOperationException("Close the current project first.");
                    if (ProjectSession.FindRecoveryDirectories(RecoveryRoot).Count > 0)
                        throw new InvalidOperationException("Recover the previous project before opening another one.");
                    var create = command.GetString() == "newProject";
                    FileDialog dialog = create
                        ? new SaveFileDialog { AddExtension = true, DefaultExt = ".aiab", OverwritePrompt = true, FileName = "New Project.aiab" }
                        : new OpenFileDialog { CheckFileExists = true, Multiselect = false };
                    dialog.Filter = "AI Agile Board project (*.aiab)|*.aiab";
                    if (dialog.ShowDialog(this) != true) break;
                    await ActivateProjectAsync(await Task.Run(() => create
                        ? ProjectSession.CreateAsync(dialog.FileName, RecoveryRoot)
                        : ProjectSession.OpenAsync(dialog.FileName, RecoveryRoot)));
                    break;
                case "recoverProject":
                    if (_session is not null) break;
                    var recoveries = ProjectSession.FindRecoveryDirectories(RecoveryRoot);
                    if (recoveries.Count > 0)
                        await ActivateProjectAsync(await Task.Run(() => ProjectSession.RecoverAsync(recoveries[0])));
                    break;
                case "retrySave":
                    if (_session is not null) await Task.Run(_session.RetrySaveAsync);
                    break;
                case "updateSettings":
                    if (_session is not null && message.RootElement.TryGetProperty("settings", out var settings))
                        await Task.Run(() => _session.UpdateSettingsAsync(settings));
                    break;
                case "closeProject":
                    await CloseProjectAsync();
                    break;
                default: return;
            }
        }
        catch (Exception exception) { _projectError = exception.Message; }
        finally
        {
            _operations.Release();
            PublishState();
        }
    }

    private async Task ActivateProjectAsync(ProjectSession session)
    {
        _session = session;
        session.Changed += SessionChanged;
        try { await RestartHostAsync(); }
        catch
        {
            await StopHostAsync();
            session.Changed -= SessionChanged;
            session.Dispose();
            _session = null;
            await RestartHostAsync();
            throw;
        }
    }

    private async Task RestartHostAsync()
    {
        Browser.Visibility = Visibility.Hidden;
        Status.Text = "Opening project…";
        Status.Visibility = Visibility.Visible;
        await StopHostAsync();
        await Task.Run(async () =>
        {
            _host = DesktopHost.Build(AppContext.BaseDirectory, _session);
            await _host.StartAsync();
            _origin = DesktopHost.Address(_host);
        });
        Browser.CoreWebView2.Navigate(_origin!.AbsoluteUri);
    }

    private async Task<bool> CloseProjectAsync()
    {
        if (_session is null) return true;
        await StopHostAsync();
        if (!await Task.Run(_session.CompleteAsync))
        {
            await RestartHostAsync();
            return false;
        }
        _session.Changed -= SessionChanged;
        _session.Dispose();
        _session = null;
        if (!_closing) await RestartHostAsync();
        return true;
    }

    private async void CloseAsync(object? sender, CancelEventArgs e)
    {
        if (_canClose) return;
        e.Cancel = true;
        if (_closing) return;
        _closing = true;
        await _operations.WaitAsync();
        try
        {
            await _startup;
            if (!await CloseProjectAsync())
            {
                _closing = false;
                MessageBox.Show(this, "The project could not be saved. Your changes are retained. Retry Save before closing.",
                    "Project save failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            _shutdown.Cancel();
            Browser.Dispose();
            await StopHostAsync();
            _shutdown.Dispose();
            _canClose = true;
            // Cleanup can complete synchronously, while WPF is still dispatching Closing.
            _ = Dispatcher.BeginInvoke(Close);
        }
        catch (Exception exception)
        {
            _closing = false;
            ShowError("The application could not close safely. Recovery data has been retained. " + exception.Message);
        }
        finally
        {
            _operations.Release();
            if (!_canClose) PublishState();
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
