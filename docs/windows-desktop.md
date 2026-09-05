# AI Agile Board for Windows

## Run

Target: Windows 11 x64. Extract **all** of `AiAgileBoard-win-x64.zip` to a folder you can write to, then double-click `AiAgileBoard.exe` inside the `AiAgileBoard` folder. Do not run it directly from the ZIP or copy only the executable. No installation, administrator access, Docker, .NET installation, or internet connection is required.

The application opens its own window. Its bundled WebView2 browser displays the React interface and the in-process .NET API listens only on a dynamically assigned loopback port. Closing the window stops the API. Launching it again while it is running activates the existing window (one instance per user in the Windows session).

## Data and replacement

Tickets remain in `data/aiagileboard.db` beside the executable. Browser state is separate in `browser-profile`. Neither directory is included in release ZIPs. Launching from another working directory does not change where data is stored.

Close the app before backing up its `data` folder or replacing application files. Back up the entire folder, including any SQLite sidecar files. To update, extract the replacement ZIP to a new folder, copy the old `data` folder into it while both copies are closed, then launch the replacement. Keep the original backup until you have verified your tickets. No automatic migration from an existing Docker volume is performed.

Advanced users may override `ConnectionStrings__DefaultConnection` in the environment or `ConnectionStrings:DefaultConnection` in `appsettings.json`. Relative SQLite paths resolve from the executable folder. Keep custom configuration when replacing binaries. Invalid or unwritable database paths display a startup error; the app does not silently create a different database.

Custom project files, file associations, and Open/Save workflows are a later story. This release retains the current database format and migrations.

## Troubleshooting

- Missing application files: extract the complete ZIP again. The `WebView2Runtime` and `wwwroot` directories are required.
- Startup or storage failure: use a writable folder, check your connection-string override and free disk space, then reopen the app.
- Browser failure: close and reopen the application. Preserve `data` when replacing binaries.

This initial package is unsigned. Signing, installers, automatic updates, and background/tray operation are outside this release. The fixed browser runtime is updated by shipping a new application package, not by installing a runtime on your computer.

## Build and verify (contributors)

The Linux solution remains focused on backend tests. The WPF project is built separately:

```powershell
docker build --target frontend-test -t ai-agile-board-frontend-test .
docker build --target backend-test -t ai-agile-board-backend-test .
docker build --target desktop-export --output type=local,dest=artifacts/desktop .
./scripts/package-desktop.ps1 -PublishDirectory artifacts/desktop
powershell -NoProfile -File scripts/test-desktop.ps1 -Package artifacts/packages/AiAgileBoard-win-x64.zip
```

The packaging script fetches the Microsoft runtime pinned in `scripts/webview2-runtime.json`, verifies SHA-256 and the executable version, and expands it without installation. `-RuntimeCab` accepts a previously downloaded CAB for offline packaging. No host SDK is needed for the Docker build. Build outputs are under `artifacts`; test copies never use a real board's database.

Windows CI publishes, packages, and runs desktop validation. Windows desktop execution is an explicitly approved exception to the normal Docker-only validation rule. For a manual check, launch the package offline on Windows 11 x64 without installed .NET or WebView2, create a ticket, open and edit it, restart, and confirm it persists. Also check second-launch activation, closing during startup, and that no process or listening API remains after closing. The package intentionally includes its own .NET and browser runtimes.

When refreshing WebView2, update its SDK and runtime manifest together, verify the new Microsoft download's checksum/version, and rerun Windows validation. Keep Microsoft's runtime license/notice files in the package.
