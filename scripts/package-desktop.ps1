param(
    [Parameter(Mandatory)][string]$PublishDirectory,
    [string]$OutputDirectory,
    [string]$RuntimeCab
)
$ErrorActionPreference = 'Stop'
if (!$OutputDirectory) { $OutputDirectory = Join-Path $PSScriptRoot '../artifacts/packages' }
$publish = (Resolve-Path -LiteralPath $PublishDirectory).Path
$manifest = Get-Content (Join-Path $PSScriptRoot 'webview2-runtime.json') -Raw | ConvertFrom-Json
foreach ($required in @('AiAgileBoard.exe', 'coreclr.dll', 'PresentationFramework.dll', 'wwwroot/index.html')) {
    if (!(Test-Path -LiteralPath (Join-Path $publish $required))) { throw "Incomplete self-contained publish: missing $required" }
}
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$output = (Resolve-Path -LiteralPath $OutputDirectory).Path
if (!$RuntimeCab) {
    $RuntimeCab = Join-Path $output "webview2-$($manifest.version).cab"
    if (!(Test-Path -LiteralPath $RuntimeCab)) {
        Invoke-WebRequest -Uri $manifest.url -OutFile $RuntimeCab
    }
}
if ((Get-FileHash -LiteralPath $RuntimeCab -Algorithm SHA256).Hash -ne $manifest.sha256) {
    throw 'WebView2 runtime checksum mismatch. Do not package this file.'
}
# Each run gets a fresh staging directory; never package user data or mutate a working app.
$stage = Join-Path $output ('staging-' + [Guid]::NewGuid().ToString('N'))
$app = Join-Path $stage 'AiAgileBoard'
$expanded = Join-Path $stage 'runtime-expanded'
New-Item -ItemType Directory -Path $app, $expanded | Out-Null
Get-ChildItem -LiteralPath $publish | Where-Object { $_.Name -notin @('data', 'recovery', 'browser-profile', 'WebView2Runtime', 'AiAgileBoard.Api.exe') -and $_.Extension -notin @('.db', '.pdb', '.aiab', '.bak', '.lock', '.tmp') } |
    Copy-Item -Destination $app -Recurse
& expand.exe $RuntimeCab '-F:*' $expanded | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Could not expand the bundled WebView2 runtime.' }
$browser = @(Get-ChildItem -LiteralPath $expanded -Filter msedgewebview2.exe -Recurse)
if ($browser.Count -ne 1) { throw 'Runtime archive must contain exactly one browser executable.' }
if ($browser[0].VersionInfo.ProductVersion -ne $manifest.version) { throw 'Runtime version does not match the manifest.' }
Copy-Item -LiteralPath $browser[0].Directory.FullName -Destination (Join-Path $app 'WebView2Runtime') -Recurse
Copy-Item -LiteralPath (Join-Path $PSScriptRoot '../docs/windows-desktop.md') -Destination (Join-Path $app 'README.md')
Copy-Item -LiteralPath (Join-Path $PSScriptRoot '../docs/project-files.md') -Destination $app
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'webview2-runtime.json') -Destination $app
$zip = Join-Path $output 'AiAgileBoard-win-x64.zip'
Compress-Archive -LiteralPath $app -DestinationPath $zip -Force
Write-Output "Package: $zip"
Write-Output "Extracted app for validation: $app"
