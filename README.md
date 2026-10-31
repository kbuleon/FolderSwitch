# Folder Switch

FolderSwitch is a small resident Windows utility that gives instant access to remembered folders and files. A global
shortcut (Ctrl + Win by default) shows a window that was built once and is only hidden between calls: tabs hold
columns, columns hold collapsible coloured groups, groups hold folders and files. One click takes the File Explorer
window, or the Open / Save As / folder-picker dialog, you called it from to that folder. From any other window the
folder opens in a new Explorer window, and a file opens with its associated application.

C# on .NET 10 with WPF, for Windows 10 and 11 (the installer is x64). Installed per user, without administrator rights.

## Main features

- **Global shortcut**, Ctrl + Win pressed alone by default. It acts when the keys are released, so the Windows
  shortcuts that start with Ctrl + Win keep working. Any other combination can be chosen. Pressed again it hides the
  window; in Edit mode, a window left behind another one comes back to the front instead, keeping Edit mode
  and taking the window you came from as the new calling window.
- **Context aware**: the calling Explorer window (its active tab on Windows 11) or file dialog (modern and legacy Open,
  Save As, folder pickers) is moved to the folder. Ctrl + click opens a new Explorer tab on Windows 11, a new window
  on Windows 10. A right-click on an item opens its menu: « Open in File Explorer » (a new window; a file's folder for
  a file), and « Open in new tab » when Ctrl + click would open one. The title bar shows the version that runs.
- **A board you organise by drag and drop** in Edit mode (« Edit » in the title bar): tabs, columns (moved by their
  ⠿ grip) of fixed or automatic width, groups with a colour that collapse, folders, files and Windows special folders (Desktop, Documents, This PC…). « + Current
  folder » remembers the folder of the window you called it from. Every change is saved at once.
- **Seven themes**, each in a light and a dark variant (or following the Windows mode), with a choice of background
  and group corners.
- **Nine languages**: English, French, Spanish, German, Italian, Portuguese (Brazil and Portugal), Dutch, Polish. A
  language is one JSON file.
- A first-run wizard (a notification then says which shortcut opens FolderSwitch), a notification-area icon, start
  with Windows, export and import of the configuration.
- An installer and updates (Velopack), updates being off until a feed address is set.

## Repository layout

| Path | Contents |
| ---- | -------- |
| `FolderSwitch.slnx` | The solution. |
| `src/FolderSwitch.Core` | Pure logic: configuration model, board editing, click routing, window classification, shortcut rules, layout, persistence, localization, themes, and the interfaces (ports) of everything that needs Windows. |
| `src/FolderSwitch.Presentation` | View models (MVVM, no WPF types). |
| `src/FolderSwitch.Windows` | Win32, COM and registry implementations of the ports: shortcut and keyboard hook, Explorer and file-dialog control, shell launch, single instance, system information. |
| `src/FolderSwitch.App` | The WPF shell and composition root: entry point, windows and views, theme resources, notification icon, updates, diagnostics, `Strings/*.json` translations. Builds `FolderSwitch.exe`. |
| `tests/FolderSwitch.Core.Tests`, `tests/FolderSwitch.Presentation.Tests`, `tests/FolderSwitch.App.Tests`, `tests/FolderSwitch.Windows.IntegrationTests` | The four test projects (see Tests). |
| `tests/DialogHost` | Helper executable that opens one file dialog of a requested kind, for the integration tests. |
| `tools/FolderSwitch.IconBuilder` | Renders the application icon into `assets/icon`. |
| `build/pack.ps1` | Publishes and packs the installer and update packages. |
| `assets/icon` | The icon (`.ico`, `.svg`, PNG sizes). |
| `Directory.Build.props`, `Directory.Packages.props`, `global.json`, `nuget.config` | Shared build settings, central package versions, .NET SDK pin, package source. |
| `.config` | The local dotnet tool (`vpk`, in `.config/dotnet-tools.json`). |
| `artifacts` | Build output (`publish`, `releases`); not versioned. |
| `ARCHITECTURE.md`, `CODEMAP.md`, `CHANGELOG.md` | Software architecture, map of the code, history of changes. |

Build and run from source (the .NET 10 SDK is enough; warnings are errors):

```powershell
dotnet build FolderSwitch.slnx -c Release
dotnet run --project src/FolderSwitch.App -c Release
```

A second copy never starts: it asks the running one to show itself, so quit an installed copy before running one from
source. A copy that Velopack did not install never checks for updates. A copy run from source uses the real
configuration, `%APPDATA%\FolderSwitch`, like an installed one, and « Start with Windows » (in the settings or the
first-run wizard) rewrites the same `Run` value, then pointing at the `bin\…\FolderSwitch.exe` of the source tree.

## Build and publish

Prerequisites: Windows and the .NET 10 SDK (`global.json`). Nothing else to install by hand: the packaging tool,
Velopack's `vpk`, is a local dotnet tool (`.config/dotnet-tools.json`) that the script restores. When upgrading, keep its
version equal to the `Velopack` package of `Directory.Packages.props`. `vpk` runs on the .NET 10 runtime that comes with
the SDK, so no roll-forward setting is needed.

```powershell
powershell -ExecutionPolicy Bypass -File build/pack.ps1
```

The script (Windows PowerShell 5.1 or PowerShell 7) publishes the application self-contained, ReadyToRun, win-x64 to
`artifacts/publish`, then packs it with `vpk` into `artifacts/releases`. The version released is the `<Version>` property of
`Directory.Build.props`; `-Version` (`x.y.z` or `x.y.z-prerelease`) releases another one, such as a prerelease. It is
also the version of the application (the settings show its `x.y.z` part). The executable is `FolderSwitch.exe`.
`-ExecutionPolicy Bypass` is for a Windows client whose policy refuses unsigned scripts.

| In `artifacts/releases`                | What it is                                               |
| -------------------------------------- | -------------------------------------------------------- |
| `FolderSwitch-win-Setup.exe`           | the installer: per user, no administrator rights         |
| `FolderSwitch-<version>-full.nupkg`    | the full package, for updates                            |
| `FolderSwitch-<version>-delta.nupkg`   | the delta from the previous version found in the folder  |
| `releases.win.json`, `RELEASES`        | the feed that lists the packages                         |
| `assets.win.json`, `FolderSwitch-win-Portable.zip` | not needed for updates                       |

Keep `artifacts/releases` between two releases: the next run builds its delta package from the previous one and adds it
to the feed. `artifacts/` is not versioned.

**Publishing.** Upload the content of `artifacts/releases` to a folder of a web server (static files only) and, before
building the release, set `UpdateFeed.Url` in `src/FolderSwitch.App/UpdateFeed.cs` to that folder's address (for
instance `https://example.org/folderswitch/`). Empty, as it is by default, the update check and the « Check for
updates » button of the settings are disabled. The address must be HTTPS (the packages come from the same server as
the feed that lists their hashes); plain HTTP is accepted only on this machine (`localhost`, `127.0.0.1`), for the
manual test below, and any other address disables the check as well. An installed copy checks about a minute after it starts, then every 24
hours. It announces a version with a notification and an « Install update » entry in the notification menu: once per
session, so again after the next start of FolderSwitch while the update is not installed, and during a session again
only for a newer version. It installs the update when the user accepts (the entry reads « Downloading update… » and the
« Check for updates » button is disabled meanwhile). A copy that Velopack did not install (a development run) never
checks. This is the only network access of FolderSwitch: the request of the feed (`releases.win.json`, with
the operating system, the architecture, the application id and the installed version in its query string) and, when the
user accepts, the download of the package. No identifier, no telemetry.

**Signing.** `-SignParams` hands the parameters of `signtool.exe` to `vpk`, which runs its own copy of it to sign the
application's files, the updater and the installer (the code signing certificate must be in the user's store, or named in
the parameters). Unsigned, SmartScreen warns about an unknown publisher.

```powershell
powershell -ExecutionPolicy Bypass -File build/pack.ps1 -SignParams '/a /fd sha256 /tr http://timestamp.digicert.com /td sha256'
```

**Checks to make by hand on a release** (no test runs the installer):

1. Quit FolderSwitch, then run `FolderSwitch-win-Setup.exe`: it installs to `%LocalAppData%\FolderSwitch` (the folder
   that Velopack owns) with no elevation prompt and starts FolderSwitch. The wizard's « Start with Windows » writes the
   `FolderSwitch` value of `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, with the path
   `%LocalAppData%\FolderSwitch\current\FolderSwitch.exe`; after signing out and in, FolderSwitch is running.
2. Uninstall it from « Apps »: the `Run` value is gone, and `%APPDATA%\FolderSwitch` (the configuration and the debug
   log, `logs\folderswitch.log`) is kept.
3. Update: set `UpdateFeed.Url` to `http://localhost:8000/`; from an empty `artifacts/releases`, pack 0.1.0 and install
   it, then pack 0.1.1 and serve `artifacts/releases` (`python -m http.server 8000` in that folder, or any static
   server). In the installed 0.1.0, press « Check for updates » in the settings: the offer for 0.1.1 appears, and
   « Install » restarts FolderSwitch on 0.1.1 (the settings show the version). Put `UpdateFeed.Url` back afterwards.

## Tests

Four xUnit projects, plus the `DialogHost` helper:

| Project | Covers |
| ------- | ------ |
| `tests/FolderSwitch.Core.Tests` | The pure logic (routing, shortcut rules, persistence, localization, themes, layout) and the shipped translation files. Runs anywhere .NET 10 does. |
| `tests/FolderSwitch.Presentation.Tests` | The view models, over fakes of every service. |
| `tests/FolderSwitch.App.Tests` | The application's own decisions, without showing any window and in temporary folders only: the exception policy and the error breaker, what a press of the shortcut does, the debug log's roll, saving and its failures, the update feed address. |
| `tests/FolderSwitch.Windows.IntegrationTests` | The real Windows services on the real desktop session. Every class is tagged `Category=Desktop`. |

The **Desktop** category drives real windows: it opens Explorer windows, file dialogs (through `DialogHost`), a message
box and a text editor, moves the foreground window, and installs the real keyboard hook. No keystroke is sent, but do
not run it while someone is using the machine. Filter it out, or run it alone:

```powershell
dotnet test FolderSwitch.slnx --filter "Category!=Desktop"   # Core, Presentation and App tests: safe at any time
dotnet test FolderSwitch.slnx --filter "Category=Desktop"    # Windows integration tests: on an idle desktop
```

Nothing automated covers the installer and the update flow: see « Checks to make by hand on a release » above.

## Diagnostic modes

```powershell
FolderSwitch.exe --self-test
FolderSwitch.exe --snapshot <directory> [--snapshot-language <code>]
```

- `--self-test` composes everything with the real services, builds and renders the main window off-screen, then exits
  without showing anything. It uses a throwaway configuration folder and never touches the user's configuration or
  log (the folder is deleted when the test passes, kept when it fails). Exit code 0: passed; 1: could not start (or
  another self-test is running); 2: not ready within 5 s; 3: an exception reached the UI thread. From the installed
  program: `(Start-Process .\FolderSwitch.exe -ArgumentList '--self-test' -Wait -PassThru).ExitCode`; from source:
  `dotnet run --project src/FolderSwitch.App -c Release -- --self-test`.
- `--snapshot <directory>` draws the main window's content and the first-run wizard into PNG files, off-screen (nothing
  appears on screen): every theme in both variants at 96 and 144 DPI, Edit mode, the menus, each page of the settings,
  the About window and the message boxes, in the language of `--snapshot-language` (French by default). It needs no running instance, touches neither the
  configuration nor the keyboard, and writes its log, `folderswitch.log`, in the output folder. Exit code 0 when every
  file was written, 1 otherwise.

## Where data lives

| What | Where |
| ---- | ----- |
| Configuration | `%APPDATA%\FolderSwitch\config.json`: indented JSON with a schema version, written at every change (a temporary file, then an atomic replace). |
| Backups | `%APPDATA%\FolderSwitch\backups\config-<yyyyMMdd-HHmmss>.json`: one at each start and one before an import replaces the configuration; the 10 most recent are kept. |
| Invalid configuration | `%APPDATA%\FolderSwitch\config.invalid-<timestamp>.json`: a damaged `config.json` is kept aside, then replaced by the latest valid backup (or by a new configuration), and the user is told. |
| Debug log | `%APPDATA%\FolderSwitch\logs\folderswitch.log`, rolling to `folderswitch.1.log` at 512 KiB. Local only. |
| Start with Windows | The `FolderSwitch` value of `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`. |
| Installed program | `%LocalAppData%\FolderSwitch`, owned by Velopack. Uninstalling removes it and the `Run` value, and keeps `%APPDATA%\FolderSwitch`. |

The configuration can be exported to a file and imported again from the Configuration page of the settings (⚙ in the title bar).

## Privacy

FolderSwitch collects nothing and sends nothing: no telemetry, no analytics, no identifier. Its debug log stays on the
machine. Its only network access is the update feed, and only when a feed address is configured (see « Publishing »
above); without one it never connects.

## Known limits

From the specification of the initial version:

- **The shortcut has no effect while a window running as administrator has the focus.** Windows does not give the
  keyboard to a program that is not elevated then. Running FolderSwitch as administrator is not supported.
- **On Windows 11, Ctrl + click opening a new Explorer tab is not guaranteed.** It relies on an Explorer command that
  Microsoft does not document and may stop working after a Windows update. FolderSwitch then opens a new window.

Also visible in the code:

- A file clicked while a file dialog is the calling window moves the dialog to the file's folder; it does not select
  the file.
- Only genuine file dialogs are driven, and only to folders such a dialog can show (This PC, libraries and network
  locations qualify, the Recycle Bin and the Control Panel do not). The move is limited to 1.5 s and fails with a
  message rather than waiting.
- The calling window's current folder is read within 50 ms. If Explorer or the dialog does not answer in time,
  FolderSwitch still knows the kind of window but « + Current folder » is unavailable for that call. It is unavailable
  too for search results, which are a query rather than a place.
- The OneDrive special folder resolves only when the OneDrive client has set its environment variable.
- File Explorer is the only supported file manager; items are folders and files; languages written left to right only.
