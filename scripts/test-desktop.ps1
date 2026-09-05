param([Parameter(Mandatory)][string]$Package)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.IO.Compression.FileSystem
Add-Type @'
using System;
using System.Runtime.InteropServices;
using System.Text;
public static class DialogTestNative {
    private delegate bool EnumWindow(IntPtr window, IntPtr parameter);
    [DllImport("user32.dll")]
    private static extern bool EnumChildWindows(IntPtr parent, EnumWindow callback, IntPtr parameter);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr window, StringBuilder name, int size);
    [DllImport("user32.dll")]
    private static extern IntPtr GetWindow(IntPtr window, uint command);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "SendMessageW")]
    public static extern IntPtr ReadText(IntPtr window, uint message, IntPtr capacity, StringBuilder text);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr SendMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "SendMessageW")]
    public static extern IntPtr SetText(IntPtr window, uint message, IntPtr wParam, string text);
    public static IntPtr FindEdit(IntPtr parent) {
        IntPtr found = IntPtr.Zero;
        EnumChildWindows(parent, (window, parameter) => {
            var name = new StringBuilder(256);
            GetClassName(window, name, name.Capacity);
            if (name.ToString() != "Edit") return true;
            found = window;
            return false;
        }, IntPtr.Zero);
        return found;
    }
    public static IntPtr FindFollowingEdit(IntPtr label) {
        var sibling = label;
        for (int i = 0; i < 8; i++) {
            sibling = GetWindow(sibling, 2);
            if (sibling == IntPtr.Zero) break;
            var name = new StringBuilder(256);
            GetClassName(sibling, name, name.Capacity);
            if (name.ToString() == "Edit") return sibling;
            var edit = FindEdit(sibling);
            if (edit != IntPtr.Zero) return edit;
        }
        return IntPtr.Zero;
    }
}
'@
$results = Join-Path $PSScriptRoot '../artifacts/desktop-test-results'
New-Item -ItemType Directory -Force -Path $results | Out-Null
foreach ($report in @('failure.txt', 'window.txt', 'passed.json')) {
    $reportPath = Join-Path $results $report
    if (Test-Path -LiteralPath $reportPath) { Remove-Item -LiteralPath $reportPath }
}
$testRoot = Join-Path $results ('Path with spaces ' + [Guid]::NewGuid().ToString('N').Substring(0, 8))
[System.IO.Compression.ZipFile]::ExtractToDirectory((Resolve-Path -LiteralPath $Package).Path, $testRoot)
$testRoot = (Resolve-Path -LiteralPath $testRoot).Path
$appDirectory = Join-Path $testRoot 'AiAgileBoard'
$exe = Join-Path $appDirectory 'AiAgileBoard.exe'
# Test-only links exercise navigation restrictions without adding controls to the product.
$index = Join-Path $appDirectory 'wwwroot/index.html'
$probe = '<div style="position:fixed;top:0;right:0;z-index:99999;background:white"><a href="https://example.invalid/">External navigation probe</a> <a href="https://example.invalid/" target="_blank">New window probe</a><button onclick="window.chrome.webview.postMessage({command: &quot;updateSettings&quot;, settings: {theme: &quot;dark&quot;}})">Set project preferences</button></div>'
[IO.File]::WriteAllText($index, [IO.File]::ReadAllText($index).Replace('</body>', "$probe</body>"))
$script:process = $null
$checks = [System.Collections.Generic.List[string]]::new()

function Wait-For([scriptblock]$Check, [string]$Description) {
    $deadline = [DateTime]::UtcNow.AddSeconds(45)
    do {
        $value = & $Check
        if ($value) { return $value }
        Start-Sleep -Milliseconds 200
    } while ([DateTime]::UtcNow -lt $deadline)
    throw "Timed out: $Description"
}
function Window {
    $script:process.Refresh()
    if ($script:process.HasExited) { throw "Desktop exited: $($script:process.ExitCode)" }
    if ($script:process.MainWindowHandle -ne 0) {
        return [System.Windows.Automation.AutomationElement]::FromHandle($script:process.MainWindowHandle)
    }
}
function Element([string]$Name, [System.Windows.Automation.ControlType]$Type) {
    $window = Window
    if (!$window) { return }
    $nameCondition = [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty, $Name)
    $typeCondition = [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ControlTypeProperty, $Type)
    $condition = [System.Windows.Automation.AndCondition]::new($nameCondition, $typeCondition)
    # Native file dialogs are owned top-level windows, not descendants of the WPF window.
    $processCondition = [System.Windows.Automation.PropertyCondition]::new(
        [System.Windows.Automation.AutomationElement]::ProcessIdProperty, $script:process.Id)
    $windows = [System.Windows.Automation.AutomationElement]::RootElement.FindAll(
        [System.Windows.Automation.TreeScope]::Children, $processCondition)
    foreach ($candidate in $windows) {
        $match = $candidate.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
        if ($match) { return $match }
    }
    $match = $window.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
    if ($match) { return $match }
    # Some Windows common-dialog providers expose native controls as panes.
    $paneCondition = [System.Windows.Automation.AndCondition]::new($nameCondition,
        [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ControlTypeProperty,
            [System.Windows.Automation.ControlType]::Pane))
    foreach ($candidate in $windows) {
        $matches = $candidate.FindAll([System.Windows.Automation.TreeScope]::Descendants, $paneCondition)
        foreach ($match in $matches) {
            if ($Type -eq [System.Windows.Automation.ControlType]::Edit -and $match.Current.ClassName -notin @('Edit', 'AppControlHost', 'Static')) { continue }
            if ($Type -eq [System.Windows.Automation.ControlType]::Button -and $match.Current.ClassName -ne 'Button') { continue }
            return $match
        }
    }
}
function Invoke-Element([string]$Name, [System.Windows.Automation.ControlType]$Type = [System.Windows.Automation.ControlType]::Button) {
    Write-Output "Invoking: $Name"
    $element = Wait-For { Element $Name $Type } $Name
    $pattern = $null
    if ($element.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern, [ref]$pattern)) {
        $pattern.Invoke()
    } elseif ($element.Current.ClassName -eq 'Button' -and $element.Current.NativeWindowHandle) {
        [DialogTestNative]::SendMessage([IntPtr]$element.Current.NativeWindowHandle, 0x00F5, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null
    } else {
        throw "Cannot invoke $Name ($($element.Current.ClassName)); patterns: $($element.GetSupportedPatterns().ProgrammaticName)"
    }
}
function Set-Field([string]$Name, [string]$Value) {
    $element = Wait-For { Element $Name ([System.Windows.Automation.ControlType]::Edit) } $Name
    $pattern = $null
    if ($element.TryGetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern, [ref]$pattern)) {
        $pattern.SetValue($Value)
    } elseif ($element.Current.NativeWindowHandle -and $element.Current.ClassName -in @('Edit', 'AppControlHost', 'Static')) {
        $edit = switch ($element.Current.ClassName) {
            'Edit' { [IntPtr]$element.Current.NativeWindowHandle }
            'AppControlHost' { [DialogTestNative]::FindEdit([IntPtr]$element.Current.NativeWindowHandle) }
            'Static' { [DialogTestNative]::FindFollowingEdit([IntPtr]$element.Current.NativeWindowHandle) }
        }
        if ($edit -eq [IntPtr]::Zero) { throw "Native edit control missing for $Name" }
        [DialogTestNative]::SendMessage($edit, 0x00B1, [IntPtr]::Zero, [IntPtr](-1)) | Out-Null
        [DialogTestNative]::SetText($edit, 0x00C2, [IntPtr](1), $Value) | Out-Null
        $text = [System.Text.StringBuilder]::new(2048)
        [DialogTestNative]::ReadText($edit, 0x000D, [IntPtr]$text.Capacity, $text) | Out-Null
        if ($text.ToString() -ne $Value) { throw "Native filename edit did not accept the test path. Actual: '$($text.ToString())'; handle: $edit" }
    } else {
        throw "Cannot set $Name ($($element.Current.ClassName)); patterns: $($element.GetSupportedPatterns().ProgrammaticName)"
    }
}
function Start-Board {
    # Launch from an unrelated working directory to test executable-relative storage.
    $script:process = Start-Process -FilePath $exe -WorkingDirectory $testRoot -WindowStyle Hidden -PassThru
    Wait-For { Window } 'desktop window' | Out-Null
}
function Select-ProjectFile([string]$Action, [string]$Path, [string]$DialogButton) {
    Invoke-Element $Action
    Set-Field 'File name:' $Path
    Invoke-Element $DialogButton
}
function Stop-Board {
    if ($script:process -and !$script:process.HasExited) {
        $script:process.CloseMainWindow() | Out-Null
        if (!$script:process.WaitForExit(15000)) { throw 'Desktop failed to shut down gracefully.' }
        if ($script:process.ExitCode -ne 0) { throw "Desktop shutdown failed: $($script:process.ExitCode)" }
    }
}

$priorConnection = $env:ConnectionStrings__DefaultConnection
$priorEndpoint = $env:Kestrel__Endpoints__Injected__Url
$priorBrowserArguments = $env:WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS
try {
    $env:ConnectionStrings__DefaultConnection = 'Data Source=data/aiagileboard.db'
    $env:Kestrel__Endpoints__Injected__Url = 'http://0.0.0.0:49011'
    # Chromium bypasses proxies for loopback; all external browser HTTP requests fail locally.
    $env:WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS = '--proxy-server=http://127.0.0.1:9'
    Start-Board
    Wait-For { Element 'New Project' ([System.Windows.Automation.ControlType]::Button) } 'project homepage' | Out-Null
    if (Test-Path (Join-Path $appDirectory 'data/aiagileboard.db')) { throw 'Homepage opened the legacy database.' }
    Invoke-Element 'Open Project'
    Invoke-Element 'Cancel'
    $projectPath = Join-Path $testRoot 'Portable board.aiab'
    Select-ProjectFile 'New Project' $projectPath 'Save'
    Wait-For { Element 'Close Project' ([System.Windows.Automation.ControlType]::Button) } 'new project opened' | Out-Null
    if (!(Test-Path -LiteralPath $projectPath)) { throw 'New Project did not create the archive.' }
    $checks.Add('Homepage, native dialog cancellation, and New Project at a user-selected path')
    Invoke-Element 'External navigation probe' ([System.Windows.Automation.ControlType]::Hyperlink)
    Invoke-Element 'New window probe' ([System.Windows.Automation.ControlType]::Hyperlink)
    Invoke-Element 'Create first ticket'
    $checks.Add('External navigation and new windows are blocked without losing the board')
    Set-Field 'Ticket title' 'Desktop smoke ticket'
    Set-Field 'Description' 'Created through the bundled browser.'
    Invoke-Element 'Submit ticket'
    # The list links to detail using the generated ticket UUID.
    $card = Wait-For {
        (Window).FindAll([System.Windows.Automation.TreeScope]::Descendants,
            [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ControlTypeProperty,
                [System.Windows.Automation.ControlType]::Hyperlink)) |
            Where-Object { $_.Current.Name -match '^[0-9a-f]{8}-[0-9a-f-]{27}$' } | Select-Object -First 1
    } 'created ticket card'
    $card.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    Set-Field 'Title' 'Desktop smoke edited'
    Invoke-Element 'Save changes'
    Wait-For { Element 'Ticket changes saved.' ([System.Windows.Automation.ControlType]::Text) } 'saved confirmation' | Out-Null
    $checks.Add('React create, detail navigation and edit with external browser HTTP unavailable')

    $listeners = @(Get-NetTCPConnection -OwningProcess $script:process.Id -State Listen)
    if ($listeners.Count -ne 1 -or $listeners[0].LocalAddress -ne '127.0.0.1') { throw 'Desktop must listen only on one loopback endpoint.' }
    $port = $listeners[0].LocalPort
    $address = "http://127.0.0.1:$port"
    if ((Invoke-RestMethod "$address/api/v1/health").status -ne 'healthy') { throw 'Health check failed.' }
    $ticket = @(Invoke-RestMethod "$address/api/v1/tickets")[0]
    if ($ticket.title -ne 'Desktop smoke edited') { throw 'UI changes did not persist through the API.' }
    $checks.Add('API contracts and loopback binding despite external endpoint configuration')

    $core = @($script:process.Modules | Where-Object { $_.ModuleName -eq 'coreclr.dll' })
    if ($core.Count -ne 1 -or $core[0].FileName -ne (Join-Path $appDirectory 'coreclr.dll')) { throw 'App is not using its bundled .NET runtime.' }
    $children = @(Get-CimInstance Win32_Process -Filter "ParentProcessId=$($script:process.Id)" | Where-Object { $_.Name -eq 'msedgewebview2.exe' })
    if (!$children -or @($children | Where-Object { !$_.ExecutablePath.StartsWith((Join-Path $appDirectory 'WebView2Runtime'), [StringComparison]::OrdinalIgnoreCase) }).Count) { throw 'App is not using its bundled browser runtime.' }
    $checks.Add('Bundled .NET and fixed WebView2 runtimes used')

    (Window).GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern).SetWindowVisualState([System.Windows.Automation.WindowVisualState]::Minimized)
    $second = Start-Process -FilePath $exe -WindowStyle Hidden -PassThru
    if (!$second.WaitForExit(10000) -or $second.ExitCode -ne 0) { throw 'Second instance did not exit successfully.' }
    Wait-For { (Window).GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern).Current.WindowVisualState -eq [System.Windows.Automation.WindowVisualState]::Normal } 'primary window restored' | Out-Null
    $checks.Add('Second launch restores the existing primary window')
    Stop-Board
    if (Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue) { throw 'API still listens after closing.' }
    foreach ($child in $children) {
        $browserProcess = Get-Process -Id $child.ProcessId -ErrorAction SilentlyContinue
        if ($browserProcess -and !$browserProcess.WaitForExit(10000)) { throw 'Bundled browser did not exit after closing.' }
    }
    $checks.Add('Graceful shutdown closes API listener')
    # Replace the entry executable from the release while closed; retain the data folder.
    $archive = [System.IO.Compression.ZipFile]::OpenRead((Resolve-Path -LiteralPath $Package).Path)
    try {
        $entry = $archive.GetEntry('AiAgileBoard/AiAgileBoard.exe')
        if (!$entry) { $entry = $archive.GetEntry('AiAgileBoard\AiAgileBoard.exe') }
        [System.IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $exe, $true)
    } finally { $archive.Dispose() }
    Start-Board
    Select-ProjectFile 'Open Project' $projectPath 'Open'
    Wait-For { Element 'Desktop smoke edited' ([System.Windows.Automation.ControlType]::Text) } 'persisted ticket after restart' | Out-Null
    $archive = [System.IO.Compression.ZipFile]::OpenRead($projectPath)
    try {
        foreach ($name in @('manifest.json', 'settings.json', 'data/aiagileboard.db')) {
            if (!$archive.GetEntry($name)) { throw "Archive missing $name" }
        }
    } finally { $archive.Dispose() }
    Invoke-Element 'Close Project'
    Wait-For { Element 'New Project' ([System.Windows.Automation.ControlType]::Button) } 'homepage after close' | Out-Null
    $checks.Add('Archive contents and persistence after reopening, executable replacement, and Close Project')

    $invalidProject = Join-Path $testRoot 'invalid.aiab'
    Set-Content -LiteralPath $invalidProject -Value 'not a zip'
    Select-ProjectFile 'Open Project' $invalidProject 'Open'
    Wait-For { (Element 'New Project' ([System.Windows.Automation.ControlType]::Button)).Current.IsEnabled } 'homepage retained after invalid archive' | Out-Null
    Select-ProjectFile 'Open Project' $projectPath 'Open'
    Wait-For { Element 'Desktop smoke edited' ([System.Windows.Automation.ControlType]::Text) } 'valid project after invalid archive' | Out-Null

    # Persist preferences through the same narrow bridge used by future settings controls.
    # The injected probe is test-only and belongs to the application origin.
    Invoke-Element 'Set project preferences' ([System.Windows.Automation.ControlType]::Button)
    Wait-For {
        $zip = [System.IO.Compression.ZipFile]::OpenRead($projectPath)
        try {
            $reader = [IO.StreamReader]::new($zip.GetEntry('settings.json').Open())
            try { ($reader.ReadToEnd() | ConvertFrom-Json).theme -eq 'dark' } finally { $reader.Dispose() }
        } finally { $zip.Dispose() }
    } 'settings autosaved into archive' | Out-Null
    $script:process.Kill()
    $script:process.WaitForExit()
    Start-Board
    Invoke-Element 'Recover Project'
    Wait-For { Element 'Desktop smoke edited' ([System.Windows.Automation.ControlType]::Text) } 'recovery after process interruption' | Out-Null
    $checks.Add('Invalid archive handling, settings persistence, and recovery after interrupted process')
    Stop-Board
    $env:ConnectionStrings__DefaultConnection = 'Data Source=data/aiagileboard.db'
    Start-Board
    Stop-Board
    $checks.Add('Closing during startup exits cleanly')
    $checks | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $results 'passed.json')
    $checks
}
catch {
    $_ | Out-String | Set-Content -LiteralPath (Join-Path $results 'failure.txt')
    if ($script:process -and !$script:process.HasExited) {
        (Window).FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition) |
            ForEach-Object { "$($_.Current.ControlType.ProgrammaticName): $($_.Current.Name)" } |
            Set-Content -LiteralPath (Join-Path $results 'window.txt')
    }
    throw
}
finally {
    if ($script:process -and !$script:process.HasExited) {
        $script:process.CloseMainWindow() | Out-Null
        if (!$script:process.WaitForExit(15000)) { $script:process.Kill() }
    }
    $env:ConnectionStrings__DefaultConnection = $priorConnection
    $env:Kestrel__Endpoints__Injected__Url = $priorEndpoint
    $env:WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS = $priorBrowserArguments
}
