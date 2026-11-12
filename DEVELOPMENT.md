# FolderSwitch — development

How to build, package, publish and test FolderSwitch, and how it behaves under the hood. For what FolderSwitch is and
how to install it, see the [README](README.md). This file is in English only.

## Documentation

- [ARCHITECTURE.md](ARCHITECTURE.md): how the software is built. The projects and their dependencies, the process
  lifecycle, threads and time budgets, the integration with Windows, persistence, localization, theming, the error
  policy, packaging and the testing strategy.
- [CODEMAP.md](CODEMAP.md): the map of the code, one line per significant file or type, folder by folder.
- [CHANGELOG.md](CHANGELOG.md): the history of changes, version by version.
- [LICENSE](LICENSE), [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt), [CONTRIBUTING.md](CONTRIBUTING.md) and
  [SECURITY.md](SECURITY.md): see the « License » and « Contributing » sections of the [README](README.md#license).

## Build and run from source

Prerequisites: Windows and the .NET 10 SDK (pinned by `global.json`). Warnings are errors.

Every `.cs`, `.xaml` and `.ps1` file starts with the license header:

```csharp
// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.
```

A C# file without it fails the build (rule IDE0073, from `.editorconfig`; the IDE's quick fix, Ctrl+., inserts it). XAML
files carry it in a `<!-- -->` comment and PowerShell files in `#` lines, by hand. Files that cannot carry a comment are
declared in `REUSE.toml`.

```powershell
dotnet build FolderSwitch.slnx -c Release
dotnet run --project src/FolderSwitch.App -c Release
```

The emoji data of the icons (`src/FolderSwitch.App/Emoji/`, copied next to the executable) is made from the Unicode files by
the maintainer's tool `tools/FolderSwitch.EmojiData`, which is not part of the build and whose output is committed; its own
`README.md` says how to run it.

A second copy never starts: it asks the running one to show itself, so quit an installed copy before running one from
source. A copy that Velopack did not install never checks for updates. A copy run from source uses the real
configuration, `%APPDATA%\FolderSwitch`, like an installed one, and « Start with Windows » (in the settings or the
first-run wizard) rewrites the same `Run` value, then pointing at the `bin\…\FolderSwitch.exe` of the source tree.

## Package and publish

Nothing to install beyond the SDK: the packaging tool, Velopack's `vpk`, is a local dotnet tool
(`.config/dotnet-tools.json`) that the script restores. When upgrading, keep its version equal to the `Velopack` package
of `Directory.Packages.props`. `vpk` runs on the .NET 10 runtime that comes with
the SDK, so no roll-forward setting is needed.

`artifacts/publish` holds `LICENSE.txt` (the GPLv3) and `THIRD-PARTY-NOTICES.txt` next to `FolderSwitch.exe`, and the
installer installs them. When a package is added or updated, or the SDK brings another runtime, update
`THIRD-PARTY-NOTICES.txt` (name, version, address, copyright and license text of each component); compare it with
`dotnet list src/FolderSwitch.App package --include-transitive` and the runtime packs of `artifacts/publish/FolderSwitch.deps.json`.
Check the license of a new package first: it must be compatible with the GPLv3 (a GPLv2-only one is not).

```powershell
powershell -ExecutionPolicy Bypass -File build/pack.ps1
```

The script (Windows PowerShell 5.1 or PowerShell 7) publishes the application self-contained, ReadyToRun, win-x64 to
`artifacts/publish`, then packs it with `vpk` into `artifacts/releases`. The version released is the `<Version>` property of
`Directory.Build.props`; `-Version` (`x.y.z` or `x.y.z-prerelease`) releases another one, such as a prerelease. It is
also the version of the application (the settings show its `x.y.z` part). `-ReleaseNotes <file>` (a Markdown file that
must exist) puts the notes of the version in the package. The executable is `FolderSwitch.exe`.
`-ExecutionPolicy Bypass` is for a Windows client whose policy refuses unsigned scripts.

| In `artifacts/releases`                | What it is                                               |
| -------------------------------------- | -------------------------------------------------------- |
| `FolderSwitch-win-Setup.exe`           | the installer: per user, no administrator rights         |
| `FolderSwitch-<version>-full.nupkg`    | the full package, for updates                            |
| `FolderSwitch-<version>-delta.nupkg`   | the delta from the previous version found in the folder  |
| `releases.win.json`, `RELEASES`        | the feed that lists the packages                         |
| `assets.win.json`, `FolderSwitch-win-Portable.zip` | not needed for updates                       |

Run alone, the script keeps what `artifacts/releases` holds and builds the delta package from the previous release found
there; `build/release.ps1` (below) rebuilds that folder from GitHub at each preparation. `artifacts/` is not versioned.

**Publishing.** The updates are the releases of `https://github.com/kbuleon/FolderSwitch` (`UpdateFeed.Url` in
`src/FolderSwitch.App/UpdateFeed.cs`, the address of the repository: any other address disables the check and the « Check
for updates » button of the settings). `build/release.ps1` makes one, from a PowerShell prompt at the root of the repository:

```powershell
powershell -ExecutionPolicy Bypass -File build/release.ps1 -Step Prepare   # build and test locally, upload nothing
powershell -ExecutionPolicy Bypass -File build/release.ps1 -Step Publish   # tag and upload what Prepare built
powershell -ExecutionPolicy Bypass -File build/release.ps1                 # All: Prepare, then Publish
```

It needs the [GitHub CLI](https://cli.github.com/) (`gh`), installed and signed in once (`gh auth login`). The release is the
`<Version>` of `Directory.Build.props` (`x.y.z` only, never a prerelease), named `FolderSwitch <version>`, on the annotated
tag `v<version>`; its notes are the version's section of `CHANGELOG.md` (titled `## <version> — `), its title in bold,
followed by the sections of the versions since the last published release, which were never released on their own.
*Prepare* runs the Core, Presentation and App tests, downloads the latest published release into an emptied
`artifacts/releases` (the base of the delta package), packs with `pack.ps1 -ReleaseNotes`, and records the version and the
commit in `artifacts/release-prepared.json`: try the installer it shows before publishing. *Publish* creates and pushes the
tag on the prepared commit, uploads `artifacts/releases` as a draft release, then sets the notes and publishes it. Both
refuse, saying what to do, unless `gh` is signed in, the branch is `main` with no uncommitted change and no untracked file
(ignored ones do not count) and the same commit as `origin/main`, and the changelog has the section; *Prepare* also refuses a
tag or a release of that version already there, and a non-empty `artifacts/releases` it did not make (it holds a marker
file, `.made-by-release-ps1`: set your own folder aside, e.g. as `artifacts/releases-before-github`), and *Publish* a missing or stale preparation, a published release and a tag on another commit. An
interrupted *Publish* is run again: a draft that holds every file is only made visible, an empty one is filled, and one
that holds only some is refused (delete the draft on GitHub, keep the tag, run it again). The token comes from
`gh auth token` when it is needed and goes to `vpk` only, in an environment variable: never shown, never written to a file.
The script never deletes a tag or a release and never changes a published release: fix a published version with a new one.

The first version that reads GitHub cannot be found by an older installed copy (its update address was empty): it is
installed by hand, with `FolderSwitch-win-Setup.exe`. From then on installed copies find each release themselves.

An installed copy asks GitHub for the releases of the repository, then for the feed of each of them, about a minute after it
starts and then every 24 hours (unless « Check for updates automatically » is unticked, which stops the timer at once and
never interrupts a check or a download in progress). It announces a version by an « Install update » entry in the
notification menu, at once, and by opening the « Install / Later » box, on top and without taking the keyboard focus (the
shortcut gives it), but only while FolderSwitch shows no window: while it is in use, the box waits and is tried again every
minute, without asking GitHub again. The box opens once per session, so again after the next start of FolderSwitch while
the update is not installed, and during a session again only for a newer version; the box opened from the menu entry
counts too. « Later » leaves the entry in the menu. It installs the update when the user accepts (the entry
reads « Downloading update… » and the « Check for updates » button is disabled meanwhile). A copy that Velopack did not
install (a development run) never checks. Without a token, the GitHub API answers 60 requests an hour per IP address, far
more than one check a day needs; no token is built into the application. The update accounts for one of the two kinds of
network access of FolderSwitch (the other is the favicons of the sites, see « Privacy »).

**Source code.** The GPL requires each published binary to come with a way to get its source (section 6). The commit of
each published version carries the tag `v<version>`, which `build/release.ps1` creates and pushes, and the release page is
in the repository itself, next to that tag.

**Signing.** `-SignParams` hands the parameters of `signtool.exe` to `vpk`, which runs its own copy of it to sign the
application's files, the updater and the installer (the code signing certificate must be in the user's store, or named in
the parameters). Unsigned, SmartScreen warns about an unknown publisher.

```powershell
powershell -ExecutionPolicy Bypass -File build/pack.ps1 -SignParams '/a /fd sha256 /tr http://timestamp.digicert.com /td sha256'
```

**Checks to make by hand on a release** (no test runs the installer):

1. Quit FolderSwitch, then run `FolderSwitch-win-Setup.exe`: it installs to `%LocalAppData%\FolderSwitch` (the folder
   that Velopack owns) with no elevation prompt and starts FolderSwitch; `current\` holds `LICENSE.txt` and
   `THIRD-PARTY-NOTICES.txt`, which the About window's links open. The wizard's « Start with Windows » writes the
   `FolderSwitch` value of `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, with the path
   `%LocalAppData%\FolderSwitch\current\FolderSwitch.exe`; after signing out and in, FolderSwitch is running.
2. Uninstall it from « Apps »: the `Run` value is gone, and `%APPDATA%\FolderSwitch` (the configuration and the debug
   log, `logs\folderswitch.log`) is kept.
3. Update: with the previous version installed (from its release), press « Check for updates » in the settings: the offer
   for the new release appears, and « Install » restarts FolderSwitch on it (the settings show the version).
4. Untick « Check for updates automatically » in the settings: the debug log says « Updates: automatic check off », and no
   check follows (tick it again: « automatic check on »).

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

## The README and its images

The README exists in the nine languages of the application: `README.md` (English, the reference) and
`README.<language>.md` (`fr`, `de`, `es`, `it`, `nl`, `pl`, `pt-BR`, `pt-PT`), with the same sections in the same order,
each linking to the others from its header. A change to one is made to all of them. Only the English « License »
section is authoritative: the others summarize it and link to it.

Each README shows the images of its language: five screenshots in `assets/screenshots/<language>/` and an animated
tour, `assets/gif/folderswitch-tour-<language>.gif`. One command draws them all again:

```powershell
powershell -ExecutionPolicy Bypass -File build/readme-assets.ps1                  # every language, about 10 minutes
powershell -ExecutionPolicy Bypass -File build/readme-assets.ps1 -Language de,pl  # some of them
```

It builds the application and `tools/FolderSwitch.DemoGif`, runs `--snapshot` in each language (the sample board of the
snapshots is translated too), copies five of the 144 DPI images, then draws the tour from them. The tour's texts are in
`tools/FolderSwitch.DemoGif/Captions/<language>.json`; the names of buttons and menus they quote are the application's
own, from `src/FolderSwitch.App/Strings/<language>.json`. The tool encodes the GIF with Magick.NET (Apache-2.0), a
build-time dependency that is not shipped with the application. The scenes are also written to
`artifacts/readme-assets/frames-<language>/` for review.

## Where data lives

| What | Where |
| ---- | ----- |
| Configuration | `%APPDATA%\FolderSwitch\config.json`: indented JSON with a schema version (2; a version 1 file is read as it is), written at every change (a temporary file, then an atomic replace). |
| Backups | `%APPDATA%\FolderSwitch\backups\config-<yyyyMMdd-HHmmss>.json`: one at each start and one before an import replaces the configuration; the 10 most recent are kept. |
| Invalid configuration | `%APPDATA%\FolderSwitch\config.invalid-<timestamp>.json`: a damaged `config.json` is kept aside, then replaced by the latest valid backup (or by a new configuration), and the user is told. |
| Favicons | `%APPDATA%\FolderSwitch\favicons\`: one image per host (`morningstar.fr.png`, the host in lower-case punycode), outside the configuration and its export; deleting the folder is harmless, the icons are fetched again. |
| Emoji data | `Emoji\` next to `FolderSwitch.exe`: `catalog.json`, `synonyms.json` and `keywords\<language>.json`. Shipped with the program, read when it starts, never downloaded; not in `%APPDATA%`. |
| Debug log | `%APPDATA%\FolderSwitch\logs\folderswitch.log`, rolling to `folderswitch.1.log` at 512 KiB. Local only. |
| Start with Windows | The `FolderSwitch` value of `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`. |
| Installed program | `%LocalAppData%\FolderSwitch`, owned by Velopack. Uninstalling removes it and the `Run` value, and keeps `%APPDATA%\FolderSwitch`. |

The configuration can be exported to a file and imported again from the Configuration page of the settings (⚙ in the title bar).

## Privacy

FolderSwitch collects nothing and sends nothing about you: no telemetry, no analytics, no identifier. Its debug log stays
on the machine. It makes two kinds of network request, and no other:

- The update check: the list of the releases of this repository from the GitHub API, then the feed of each release, a
  minute after FolderSwitch starts and then every 24 hours, unless « Check for updates automatically » is unticked (and
  when the user presses « Check for updates » or accepts an update, which also downloads the package). GitHub sees the IP
  address and the `User-Agent` header that Velopack sends, under GitHub's own privacy statement. FolderSwitch adds no
  token and no identifier, and nothing from the board or the configuration is in a request.
- The favicon of a web site, when a site is added to a board, and once per session for a site that has none in the
  cache. It is a plain `GET` of the site's own page, then of the icon the page declares or `/favicon.ico`, to the site
  itself, up to 5 redirects, with no cookie and the single header `User-Agent: FolderSwitch/<version>`; it gives up after
  5 s in all, and reads 256 KiB at most from any response. Nothing from the board or the configuration is in a request.
  Reading the address of the page a browser shows (for « Current URL ») stays on the machine.

The emoji data of the icons is shipped with the program: nothing is downloaded for it. So is the cup of Ko-fi's button
in Edit mode: only a click on it opens the Ko-fi page, in the browser.

With automatic update checks turned off (or in a copy Velopack did not install) and without a site on the board,
FolderSwitch never connects on its own.

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
- Only the `http`, `https`, `ftp` and `file` schemes are accepted for a site. A favicon that is neither an ICO nor a bitmap (PNG, GIF,
  JPEG, BMP), such as an SVG, shows the globe.
- Going back to an earlier version of FolderSwitch loses the changes made since the update: it does not read a version 2
  configuration, sets it aside and starts again from the latest backup it can read. A 1.7.0 reads a configuration that has
  icons, ignores them and drops them when it saves; nothing else is lost.
- From a file dialog only folders can be used: a file or a site is greyed out, and a file no longer moves the dialog to
  its folder. Edit mode is not affected.
- Only genuine file dialogs are driven, and only to folders such a dialog can show (This PC, libraries and network
  locations qualify, the Recycle Bin and the Control Panel do not). The move is limited to 1.5 s and fails with a
  message rather than waiting.
- The calling window's current folder is read within 50 ms. If Explorer or the dialog does not answer in time,
  FolderSwitch still knows the kind of window but « Current folder » is unavailable for that call. It is unavailable
  too for search results, which are a query rather than a place.
- The OneDrive special folder resolves only when the OneDrive client has set its environment variable.
- Emoji icons are drawn in one colour, and some are less readable that way than others. The list leaves out flags, skin tone
  variants and composed emojis (families, professions), which WPF cannot assemble, and the few emojis that do not read well in
  one colour.
- The emojis offered, and shown, are those the machine's `Segoe UI Emoji` can draw: Windows 10's font has fewer than
  Windows 11's, so an icon chosen on 11 is hidden on 10 (it stays in the configuration and comes back where the font has it).
  The drawing on Windows 10 has not been tried on a real machine.
- An icon is suggested only from the words of the table and the Unicode keywords: a proper name or a rare word gets none, and the
  suggestion is made once, when the name is validated (it does not follow a later change of language).
- File Explorer is the only supported file manager; items are folders, files and web sites; languages written left to right only.
