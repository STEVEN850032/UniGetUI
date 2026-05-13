# UniGetUI TUI

`UniGetUI.Tui` is an experimental Consolonia-based terminal shell for UniGetUI. It is intentionally isolated from the Avalonia desktop project so the terminal UI can use Consolonia without coupling to desktop-only windows, tray icons, WebView, native file pickers, or desktop notification APIs.

## Build and run

From the repository root:

```powershell
dotnet build .\src\UniGetUI.Tui\UniGetUI.Tui.csproj
dotnet run --project .\src\UniGetUI.Tui\UniGetUI.Tui.csproj
```

The TUI uses the same package engine, settings, package loaders, install options, bundle APIs, and operation pipeline as the desktop shells.

## Current surfaces

| Surface | Terminal behavior |
| --- | --- |
| Dashboard | Shows package-manager readiness, executable paths, and package counts. |
| Discover | Enter a query, search available packages, inspect details, install, and add packages to a bundle. |
| Updates | Reload available updates, view package details, queue updates, uninstall, or add entries to a bundle. |
| Installed | Reload installed packages, inspect details, uninstall, reinstall through install action where applicable, or add entries to a bundle. |
| Bundles | View the in-memory bundle, add/remove selected packages, import/export bundles with a typed path, and queue bundle installs. |
| Settings | Edit selected shared UniGetUI settings directly with terminal controls. |
| Logs | View application logs and persisted operation history. |
| Help | Shows terminal-native usage guidance instead of an embedded WebView. |
| Backup/Auth | Shows backup directory and GitHub login status; terminal-safe controls edit the related settings. |

## Terminal-specific adaptations

- File and folder pickers are replaced by typed paths.
- WebView-based content is represented by text and links.
- Tray, toast, and desktop notification behavior is replaced by status text and the operation panel.
- Browser-based GitHub authentication remains a desktop-first flow; the TUI exposes status and settings without launching graphical OAuth prompts.
- Elevation is delegated to the shared package-operation layer, but terminal-friendly elevation behavior should be preferred for future TUI-specific refinements.

## Implementation notes

- Shared package list filtering, sorting, source/manager filtering, checked selection, and subtitle generation live in `UniGetUI.PackageEngine.PackageLoader.PackageListQuery`.
- Terminal operations are tracked by `TuiOperationRegistry`, which records output and writes operation history without desktop notifications.
- The TUI project should not reference `UniGetUI.Avalonia`.
