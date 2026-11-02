# Folder Switch

FolderSwitch is a small resident Windows utility that gives instant access to remembered folders and files. A global
shortcut (Ctrl + Win by default) shows a window that was built once and is only hidden between calls: tabs hold
columns, columns hold collapsible coloured groups, groups hold folders, files and web sites. One click takes the File Explorer
window, or the Open / Save As / folder-picker dialog, you called it from to that folder. From any other window the
folder opens in a new Explorer window, a file opens with its associated application and a site in the default browser.

C# on .NET 10 with WPF, for Windows 10 and 11 (the installer is x64). Installed per user, without administrator rights.

## Screenshots

The board, as the shortcut shows it (Fluent theme, light):

![The FolderSwitch window: three tabs, three columns of coloured groups of folders and files](assets/screenshots/board.png)

Edit mode, where tabs, columns, groups and items are added, renamed, recoloured and moved by drag and drop (Cards
theme, dark):

![Edit mode: grips, colour and delete buttons on every group, an « Add ▾ » button under each group, « + Group » and « + Column » buttons](assets/screenshots/edit-mode.png)

The « Add ▾ » menu of a group, called from Firefox: the page shown by the browser comes first, then the folder picker,
the special folders, the file picker and the address prompt. Called from Explorer, « Current folder » comes first:

![The Add menu of the Finance group, called from Firefox: Current URL, Folder…, Special folders, File…, URL…](assets/screenshots/add-menu.png)

The Appearance page of the settings: the mode, the theme, the background colour and the group corners:

![The Appearance page of the settings, with the Light, Dark and Auto modes and the seven themes](assets/screenshots/settings-appearance.png)

These images are drawn by the `--snapshot` diagnostic mode (see « Diagnostic modes » below).

## Main features

- **Global shortcut**, Ctrl + Win pressed alone by default. It acts when the keys are released, so the Windows
  shortcuts that start with Ctrl + Win keep working. Any other combination can be chosen. Pressed again it hides the
  window; in Edit mode, a window left behind another one comes back to the front instead, keeping Edit mode
  and taking the window you came from as the new calling window.
- **Context aware**: the calling Explorer window (its active tab on Windows 11) or file dialog (modern and legacy Open,
  Save As, folder pickers) is moved to the folder. Ctrl + click opens a new Explorer tab on Windows 11, a new window
  on Windows 10. A right-click on an item opens its menu: « Open in File Explorer » (a new window; a file's folder for
  a file), and « Open in new tab » when Ctrl + click would open one; for a site, « Copy address ». The title bar shows
  the version that runs. From a file dialog only folders can be used: files and sites are greyed out, with a tooltip.
- **A board you organise by drag and drop** in Edit mode (« Edit » in the title bar): tabs, columns (moved by their
  ⠿ grip) of fixed or automatic width, groups with a colour that collapse, folders, files, web sites and Windows special folders (Desktop, Documents, This PC…). A group's
  « Add ▾ » menu starts with what the window you called FolderSwitch from shows: « Current folder » from Explorer or a
  file dialog, added in one click, « Current URL » from Firefox, Chrome or Edge, whose address opens in the « URL… » prompt to be
  trimmed (of a session id, say) before it is added. It goes on with « Folder… », « Special folders »,
  « File… » and « URL… » (an address typed or pasted; the clipboard's is offered). A column, group or item held over another tab for a
  moment shows that tab, where it can be dropped. A right-click on a tab, a group's header or an item offers « Rename »
  (F2). Every change is saved at once. The window keeps the width of its widest tab: the tabs stay in place when you
  switch.
- **Icons that say what an item is**: a file shows the icon Windows gives its type, found without touching the file; a
  site shows its favicon, fetched once in the background when the site is added (and tried once more per session when
  the cache has none), and a globe until then or when it has none.
- **Seven themes**, each in a light and a dark variant (or following the Windows mode), with a choice of background
  and group corners.
- **Nine languages**: English, French, Spanish, German, Italian, Portuguese (Brazil and Portugal), Dutch, Polish. A
  language is one JSON file.
- A first-run wizard (a message then says which shortcut opens FolderSwitch), a notification-area icon, start
  with Windows, export and import of the configuration.
- An installer and updates (Velopack), updates being off until a feed address is set. Once an installation, a repair or
  an update ends, a message box says so, with the version and the shortcut that opens FolderSwitch.

## Documentation

- [ARCHITECTURE.md](ARCHITECTURE.md): how the software is built. The projects and their dependencies, the process
  lifecycle, threads and time budgets, the integration with Windows, persistence, localization, theming, the error
  policy, packaging and the testing strategy.
- [CODEMAP.md](CODEMAP.md): the map of the code, one line per significant file or type, folder by folder.
- [CHANGELOG.md](CHANGELOG.md): the history of changes, version by version.

## Build and run from source

Prerequisites: Windows and the .NET 10 SDK (pinned by `global.json`). Warnings are errors.

```powershell
dotnet build FolderSwitch.slnx -c Release
dotnet run --project src/FolderSwitch.App -c Release
```

A second copy never starts: it asks the running one to show itself, so quit an installed copy before running one from
source. A copy that Velopack did not install never checks for updates. A copy run from source uses the real
configuration, `%APPDATA%\FolderSwitch`, like an installed one, and « Start with Windows » (in the settings or the
first-run wizard) rewrites the same `Run` value, then pointing at the `bin\…\FolderSwitch.exe` of the source tree.

## Package and publish

Nothing to install beyond the SDK: the packaging tool, Velopack's `vpk`, is a local dotnet tool
(`.config/dotnet-tools.json`) that the script restores. When upgrading, keep its version equal to the `Velopack` package
of `Directory.Packages.props`. `vpk` runs on the .NET 10 runtime that comes with
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
checks. The update accounts for one of the two kinds of network access of FolderSwitch (the other is the favicons of the
sites, see « Privacy »): the request of the feed (`releases.win.json`, with the operating system, the architecture, the
application id and the installed version in its query string) and, when the user accepts, the download of the package.
No identifier, no telemetry.

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

Four xUnit projects (Core, Presentation, App, Windows integration), plus the `DialogHost` helper; the testing strategy
of [ARCHITECTURE.md](ARCHITECTURE.md) says what each one covers. Every class of the Windows integration tests is tagged
`Category=Desktop`, except three that touch no window (the browser reading rule, its wait per window, the file type
icons).

The **Desktop** category drives real windows: it opens Explorer windows, file dialogs (through `DialogHost`), a message
box, a text editor and, for each of Firefox, Chrome and Edge that is installed, a browser with a throwaway profile on
pages served by the test itself (a browser that is not installed skips its tests), moves the foreground window, and
installs the real keyboard hook. No keystroke is sent, but do not run it while someone is using the machine. Filter it out, or run it alone:

```powershell
dotnet test FolderSwitch.slnx --filter "Category!=Desktop"   # Core, Presentation, App and the windowless Windows tests: safe at any time
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
| Configuration | `%APPDATA%\FolderSwitch\config.json`: indented JSON with a schema version (2; a version 1 file is read as it is), written at every change (a temporary file, then an atomic replace). |
| Backups | `%APPDATA%\FolderSwitch\backups\config-<yyyyMMdd-HHmmss>.json`: one at each start and one before an import replaces the configuration; the 10 most recent are kept. |
| Invalid configuration | `%APPDATA%\FolderSwitch\config.invalid-<timestamp>.json`: a damaged `config.json` is kept aside, then replaced by the latest valid backup (or by a new configuration), and the user is told. |
| Favicons | `%APPDATA%\FolderSwitch\favicons\`: one image per host (`morningstar.fr.png`, the host in lower-case punycode), outside the configuration and its export; deleting the folder is harmless, the icons are fetched again. |
| Debug log | `%APPDATA%\FolderSwitch\logs\folderswitch.log`, rolling to `folderswitch.1.log` at 512 KiB. Local only. |
| Start with Windows | The `FolderSwitch` value of `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`. |
| Installed program | `%LocalAppData%\FolderSwitch`, owned by Velopack. Uninstalling removes it and the `Run` value, and keeps `%APPDATA%\FolderSwitch`. |

The configuration can be exported to a file and imported again from the Configuration page of the settings (⚙ in the title bar).

## Privacy

FolderSwitch collects nothing and sends nothing about you: no telemetry, no analytics, no identifier. Its debug log stays
on the machine. It makes two kinds of network request, and no other:

- The update feed, only when a feed address is configured (see « Publishing » above).
- The favicon of a web site, when a site is added to a board, and once per session for a site that has none in the
  cache. It is a plain `GET` of the site's own page, then of the icon the page declares or `/favicon.ico`, to the site
  itself, up to 5 redirects, with no cookie and the single header `User-Agent: FolderSwitch/<version>`; it gives up after
  5 s in all, and reads 256 KiB at most from any response. Nothing from the board or the configuration is in a request.
  Reading the address of the page a browser shows (for « Current URL ») stays on the machine.

Without a configured feed and without a site on the board, FolderSwitch never connects.

## Known limits

Imposed by Windows:

- **The shortcut has no effect while a window running as administrator has the focus.** Windows does not give the
  keyboard to a program that is not elevated then. Running FolderSwitch as administrator is not supported.
- **On Windows 11, Ctrl + click opening a new Explorer tab is not guaranteed.** It relies on an Explorer command that
  Microsoft does not document and may stop working after a Windows update. FolderSwitch then opens a new window.

In the current version:

- « Current URL » works with Firefox, Chrome and Edge only, and relies on what they expose to Windows accessibility
  (UI Automation): a browser update may break it, and the address can still be typed (« URL… »). A browser run as
  administrator cannot be read. The first reading of a browser session can be slow, so the line may appear after a moment,
  or not at all; the browser is not otherwise touched, but reading it may turn on its accessibility mode until it closes.
  A site opens in the default browser, not in the browser the address came from.
  From a Chrome or Edge window that is hidden or fully covered, only the address bar can be read, and it hides the scheme:
  an `http` page is then stored as `https` (remove it and add it again with « URL… »).
- Only the `http` and `https` schemes are accepted for a site. A favicon that is neither an ICO nor a bitmap (PNG, GIF,
  JPEG, BMP), such as an SVG, shows the globe.
- Going back to an earlier version of FolderSwitch loses the changes made since the update: it does not read a version 2
  configuration, sets it aside and starts again from the latest backup it can read.
- From a file dialog only folders can be used: a file or a site is greyed out, and a file no longer moves the dialog to
  its folder. Edit mode is not affected.
- Only genuine file dialogs are driven, and only to folders such a dialog can show (This PC, libraries and network
  locations qualify, the Recycle Bin and the Control Panel do not). The move is limited to 1.5 s and fails with a
  message rather than waiting.
- The calling window's current folder is read within 50 ms. If Explorer or the dialog does not answer in time,
  FolderSwitch still knows the kind of window but « Current folder » is unavailable for that call. It is unavailable
  too for search results, which are a query rather than a place.
- The OneDrive special folder resolves only when the OneDrive client has set its environment variable.
- File Explorer is the only supported file manager; items are folders, files and web sites; languages written left to right only.
