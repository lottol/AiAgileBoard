# Portable project files

The Windows desktop app starts at a project homepage. **New Project** chooses where to create an `.aiab` file; **Open Project** selects an existing file. One project is open at a time. **Close Project** returns to the homepage. File dialog cancellation changes nothing.

An `.aiab` is a ZIP containing all board data and saved user preferences. Move or copy it to another computer to bring the project with you. Legacy standalone databases are not imported automatically; the previous `data` folder is left untouched. Docker continues using its configured SQLite database.

## Format version 1

| Entry | Contents |
| --- | --- |
| `manifest.json` | JSON object containing `"formatVersion": 1`. |
| `settings.json` | JSON object holding saved user preferences; initially `{}` because no preference controls currently exist. |
| `data/aiagileboard.db` | Consistent SQLite snapshot containing tickets, comments, workflow states, and EF migration history. |

All saved user preferences must use project settings, not browser storage or an external configuration file. The desktop bridge exposes `updateSettings` for future settings controls, with the full settings object as its payload. Unknown preference keys are preserved. Project-owned assets must be added to the versioned format when those features are introduced.

Version 1 accepts exactly these three entries, up to 512 MiB total expanded size and 1 MiB per JSON entry. Duplicate names, unknown entries, path traversal, missing files, unsupported format/schema versions, malformed settings, and invalid SQLite databases are rejected. Opening validates the database and applies known EF migrations to the working copy before activation. The previous archive is preserved as `.aiab.bak` when the upgraded archive is saved.

The working database location and loopback API port are determined at runtime. Browser caches are disposable. Neither is a user setting. Desktop ignores server connection-string overrides; Docker still honors them.

## Saving and recovery

Each board mutation or settings update automatically saves a new complete archive. SQLite's backup API produces a consistent snapshot, including committed WAL-backed changes. A temporary sibling file is flushed and atomically replaces the project; `.aiab.bak` retains the previous valid archive. A `.aiab.lock` sidecar prevents cooperating app sessions from opening the same file concurrently.

The toolbar shows saving, saved, or failed status. If packaging fails after a ticket has committed, the ticket remains in the working copy and its HTTP operation remains successful. Further writes receive HTTP 503 until **Retry Save** succeeds. Retry packages the existing working copy and does not resubmit the ticket. Normal close waits for writes and saving; a save failure keeps the project open.

Working copies live under `recovery/<session-id>` beside the executable. They include an internal `session.json` pointing to the archive; this is recovery bookkeeping, not a saved preference. After an interruption, the homepage offers **Recover Project** before opening anything else. Successful close removes the recovery record and working copy. Do not remove recovery data while there are unsaved changes.

If the destination is unavailable, restore access and select **Retry Save**. If the archive changed externally, saving stops instead of overwriting it. Preserve both the external archive and the entire recovery directory before resolving the conflict; retain the external copy elsewhere and restore the expected archive generation to its original location before retrying. Automatic merging and Save As are not implemented.

## Desktop bridge

Only messages from the active application origin are accepted. Commands are `getState`, `newProject`, `openProject`, `closeProject`, `retrySave`, `recoverProject`, and `updateSettings`. File selection stays in native dialogs; bridge messages cannot supply filesystem paths. `projectState` notifications contain the filename, save status, error, complete settings object, and recovery availability. Ticket routes and successful payloads are unchanged.

## Validation

Docker backend integration tests exercise archive validation, WAL snapshots, migrations, isolation, persistence failures, endpoint behavior, and recovery on both sides of archive replacement. Frontend tests cover the homepage, cancellation, recovery, blocked edits, retry, and settings commands. `scripts/test-desktop.ps1` validates native dialogs, reopening after restart, settings autosave, invalid archives, and recovery in the packaged Windows application.
