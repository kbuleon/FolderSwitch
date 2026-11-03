History of **software changes** to the project's programs. Changes that only create `docs` files are not tracked here.
This file is maintained by the agent: one section per version, the newest first, titled with its number and what it changes, its date and its work item below. The log is **concise**. It is not documentation.

------

## 1.10.3 — Movable window, opens at the cursor
<sub>2026-11-04 · FOS-0015</sub>

The window moves by its title bar (anywhere but on its buttons), in normal use as in Edit mode; dropped partly off-screen, it slides back inside the work area of the screen it lands on, where it now stays when its content changes size or its screen's scale changes. The shortcut opens it centered on the mouse cursor, shifted just enough to be fully visible; the notification icon (click, « Open », « Customize ») and a second launch still center it on the screen. No position is kept from one opening to the next.

## 1.10.2 — Changelog by version
<sub>2026-11-03</sub>

`CHANGELOG.md` has one section per version, titled with its number, with its date and its work item in small type below, instead of one section per date. `build/release.ps1` takes the notes of a version from its section: its title in bold, then the text below it.

## 1.10.1 — Ko-fi button easier to read
<sub>2026-11-03 · FOS-0014</sub>

The button of Edit mode is drawn by FolderSwitch instead of shown as Ko-fi's image, whose text was too small at the title bar's height: Ko-fi's yellow and cup, then a bold text as large as the name, in the language shown (« Offrez-moi un café »…). Same height, same place; only the cup is shipped now (`assets/ko-fi/kofi-cup.png`). Nine languages.

## 1.10.0 — Ko-fi button in Edit mode
<sub>2026-11-03 · FOS-0014</sub>

Edit mode shows Ko-fi's « Buy me a coffee » button, the README's, after « FolderSwitch <version> » in the title bar, away from « Done »; a click opens the author's Ko-fi page in the default browser. It is as high as the title bar's buttons, never widens the window and is left out when the title bar lacks room. The image is shipped in the executable (Ko-fi Labs' own, not under the GPL: `THIRD-PARTY-NOTICES.txt`, `REUSE.toml`); nothing is fetched to show it. Its tooltip in nine languages.

## 1.9.1 — French « Done » button
<sub>2026-11-03</sub>

The button that leaves Edit mode reads « Terminer », an action like « Modifier », instead of « Terminé ».

## 1.9.0 — Updates from GitHub releases
<sub>2026-11-03 · FOS-0011</sub>

Installed copies look for updates in the GitHub releases of `kbuleon/FolderSwitch` (stable ones, no token) instead of a feed folder; the `localhost` test address is gone, and any address that is not that of a GitHub repository turns the check off. A « Check for updates automatically » box in the settings, ticked by default, unticked stops the automatic checks (the first one a minute after the start, then daily) at once, without a restart (the « Check for updates » button still works, and an import brings its value); the configuration stays schema 2, and a 1.8.0 ignores the setting. `build/release.ps1` (`Prepare`, `Publish`, `All`) tests, tags `v<version>` and publishes a release with the `CHANGELOG.md` entry as notes; `pack.ps1 -ReleaseNotes`. Nine languages.

## 1.8.0 — Emoji icons for tabs and groups
<sub>2026-11-02 · FOS-0010</sub>

A tab and a group can show an emoji to the left of their name, drawn in one colour, the text's, in « Segoe UI Emoji ». The icon is given from the name when it is typed, at creation and at each rename (the first-run content too, never « New tab » nor « New group »): from FolderSwitch's own table of common folder and site words, then from the Unicode (CLDR) keywords of the current language, then English's; none when no word leads to an emoji. In Edit mode a click on the icon (or on its empty slot), or « Icon… » in the right-click menu, opens a picker: a filter on keywords, « Automatic », « No icon » and the categories. An icon chosen by hand never changes on a rename; existing tabs and groups are given none. An icon the machine's emoji font cannot draw is hidden and kept in the configuration. Emoji data (781 emojis, Unicode Emoji 18.0 and CLDR 49, nine languages) made by the new `tools/FolderSwitch.EmojiData`, shipped in `Emoji\` next to the executable and loaded in the background, never fetched; under the Unicode License v3 (`THIRD-PARTY-NOTICES.txt`, `REUSE.toml`). Configuration schema stays 2 (`icon`, `iconChosen`); a 1.7.0 reads the file, ignores the icons and drops them when it saves. Nine languages.

## 1.7.0 — GPL-3.0-or-later license
<sub>2026-11-02 · FOS-0007</sub>

FolderSwitch is free software under the GPL-3.0-or-later, Copyright (C) 2026 Karl BULEON, with additional terms (section 7): attribution kept, no « FolderSwitch » name nor icon for a modified version, no trademark right. `LICENSE` (the GPLv3 text), `THIRD-PARTY-NOTICES.txt` (MIT notices of the packages and runtimes; a pointer for Velopack's Rust crates), `REUSE.toml`, `CONTRIBUTING.md` (no code contributions) and a « License » section in the README. Every `.cs`, `.xaml` and `.ps1` file starts with an SPDX header; a C# file without it fails the build (IDE0073). The About window has a shorter description and shows the copyright, the license and the absence of warranty, with links to `LICENSE.txt` and `THIRD-PARTY-NOTICES.txt`, now installed next to `FolderSwitch.exe`; nine languages. The executable's properties show the copyright.

## 1.6.0 — Current URL checked before it is added
<sub>2026-11-02 · FOS-0009</sub>

« Current URL » in the Add menu no longer adds the page at once; it opens the « URL… » prompt with the page's address, which can be edited (to remove a session id, say) before it is added, still named by the page's title. Cancelling adds nothing. The menu's tip says so, in nine languages.

## 1.5.0 — Web sites, file type icons and the Add menu
<sub>2026-11-01 · FOS-0004</sub>

A third kind of item, the web site, next to the folder and the file: it opens in the default browser, and its right-click menu offers « Copy address ». A group has one « Add ▾ » button in place of « + Folder », « + File » and « + Current folder »; its menu starts with what the calling window shows, « Current folder » from Explorer or a file dialog, « Current URL » from Firefox, Chrome or Edge (read through UI Automation while the menu is open, retried for 2 s, no extension), then « Folder… », « Special folders », « File… » and « URL… » (a prompt that starts with the clipboard's address), and ends with a line that says where FolderSwitch was called from. A file shows the icon Windows gives its type, found without touching the file; a site shows its favicon, fetched in the background into `%APPDATA%\FolderSwitch\favicons` (5 s, 256 KiB, no cookie, `User-Agent: FolderSwitch/<version>`; one more try per session for a site missing from the cache), the globe meanwhile. Called from a file dialog, files and sites are greyed out with a tooltip, and a file no longer moves the dialog to its folder; the context line at the bottom of the window is gone. Configuration schema 2 (`url` items): a version 1 file is read as is, a previous FolderSwitch sets a version 2 file aside. The update feed is no longer the only host contacted. Nine languages.

## 1.4.0 — Installation and update message in a box
<sub>2026-11-01</sub>

The message shown once an installation, a repair (the installer run again) or an update ends is now a message box, read at the user's pace, instead of a notification that disappears; it comes before the other startup messages. A first installation shows it too, after the wizard, in place of the ready notification, which remains only for a first run without an installation.

## 1.3.0 — Notification after an installation or an update
<sub>2026-11-01</sub>

The installer and the updater end without a word; FolderSwitch now says, once ready, « FolderSwitch has been updated to version x.y.z » or « FolderSwitch x.y.z has been installed », then « Press Ctrl + ⊞ Win to open FolderSwitch » (the shortcut in effect, left out when it could not be registered). A first installation keeps the wizard's ready notification only. From Velopack's first-run and restarted hooks; nine languages.

## 1.2.0 — Drag and drop to another tab; Edit-mode right-click menu
<sub>2026-11-01</sub>

In Edit mode, a column, a group or an item dragged onto another tab and held there for half a second shows that tab, where it can be dropped anywhere (an item needs a group); a drag that drops nothing goes back to its tab. A tab keeps at least one column: the last one has no ✕ and cannot be dragged out, and a tab without a column, from an older configuration, gets an empty one when loaded. A right-click on a tab, a group's header or an item, in Edit mode, opens a menu with « Rename » (F2); tabs lose their selection frame (the underline already marks the tab shown) and F2 without a selection renames the tab shown. The window keeps the width of its widest tab, so the tabs no longer move when another one is shown; only the height follows the tab. A column fits its content again after a change of theme.

## 1.1.0 — Columns move by drag and drop
<sub>2026-10-31</sub>

In Edit mode, each column has a ⠿ grip on the middle of its top edge; dragging it moves the column, with its groups and its width, within its tab (an insertion line shows where). Edit mode leaves room above the columns, so the grip, the ✕ of an empty column and the width being dragged are no longer cut off at the top.

## 1.0.0 — First stable version; item menu and version in the title
<sub>2026-10-31 · FOS-0003</sub>

A right-click on an item, in normal use, opens a menu: « Open in File Explorer » (a new window; for a file, « Open file location », its folder) and « Open in new tab » with « Ctrl+click », only when a Ctrl+click opens a tab (a folder, called from Explorer, Windows 11). The title bar shows the version after « FolderSwitch ».

## 0.2.1 — Fix: Ctrl+click opened a stray window
<sub>2026-10-31</sub>

The new tab stayed on Home and the folder opened in a new Explorer window when Ctrl was still held at the navigation; the new tab is now navigated with `IShellBrowser.BrowseObject` (same browser), which ignores Ctrl. Title bar buttons share one height.

## 0.2.0 — Settings in one window; « Customize » becomes « Edit »
<sub>2026-10-31</sub>

* **Settings in one window** (FOS-0002): ⚙ and (?) always in the title bar; one settings window drawn over the main window on a dimmed backdrop, with categories (General, Shortcut, Appearance, Configuration) and ✕ / Escape / backdrop click to close; the separate appearance button is gone. « Apply » is disabled while the shortcut shown is the one in effect, with a « ✓ This shortcut is active » note; a shortcut refused at startup can be applied again. New About window. The shortcut goes to the test zone only on the Shortcut page.
* **« Customize » becomes « Edit »** (FOS-0002): « Modifier » in French, in the nine languages, tray menu included; title bar order « Edit », ⚙, (?), ✕; messages that sent the user to « Customize mode » for the settings now point to the settings.
* **Version in the repository**: `<Version>` property in `Directory.Build.props` (0.1.1, the last release); `build/pack.ps1` releases it by default, `-Version` now optional.

## 0.1.1 — Initial version
<sub>2026-10-30 · FOS-0001 · published as 0.1.0, then 0.1.1</sub>

* **Initial version**: resident WPF utility opened by a global shortcut (default Ctrl + Win) on a pre-built hidden window of tabs, columns and groups of folders and files; a click navigates the calling Explorer window or file dialog, else opens a new Explorer window or the file. Customize mode with drag and drop, 7 themes, 9 languages, first-run wizard, JSON configuration with backups, notification-area icon, Velopack installer and updates, `--self-test` and `--snapshot` modes.
* **Final review fixes**: file-dialog text kept behind an unanswered OK; keyboard hook re-armed (resume, every 5 min) on a high-priority thread; shortcut recalls a Customize window left behind instead of hiding it; mapped network drives fail fast like UNC shares; wrapping tab bar; test zone tests only the applied shortcut; ready notification after the wizard; activation problems logged, critical exceptions let through; shell errors not doubled; navigation falls back to a new window; layout key names; flushed save, atomic export, tolerant backup names; log roll failure survived; HTTPS-only update feed; App test project.
