History of **software changes** to the project's programs. Changes that only create `docs` files are not tracked here.
This file is maintained by the agent. The log is timestamped and **concise**. It is not documentation.

------

## 2026-10-31

* **1.0.0 — First stable version; item menu and version in the title** (FOS-0003): a right-click on an item, in normal use, opens a menu: « Open in File Explorer » (a new window; for a file, « Open file location », its folder) and « Open in new tab » with « Ctrl+click », only when a Ctrl+click opens a tab (a folder, called from Explorer, Windows 11). The title bar shows the version after « FolderSwitch ».
* **0.2.1 — Fix: Ctrl+click opened a stray window**: the new tab stayed on Home and the folder opened in a new Explorer window when Ctrl was still held at the navigation; the new tab is now navigated with `IShellBrowser.BrowseObject` (same browser), which ignores Ctrl. Title bar buttons share one height.
* **0.2.0 — Settings in one window** (FOS-0002): ⚙ and (?) always in the title bar; one settings window drawn over the main window on a dimmed backdrop, with categories (General, Shortcut, Appearance, Configuration) and ✕ / Escape / backdrop click to close; the separate appearance button is gone. « Apply » is disabled while the shortcut shown is the one in effect, with a « ✓ This shortcut is active » note; a shortcut refused at startup can be applied again. New About window. The shortcut goes to the test zone only on the Shortcut page.
* **« Customize » becomes « Edit »** (FOS-0002): « Modifier » in French, in the nine languages, tray menu included; title bar order « Edit », ⚙, (?), ✕; messages that sent the user to « Customize mode » for the settings now point to the settings.
* **Version in the repository**: `<Version>` property in `Directory.Build.props` (0.1.1, the last release); `build/pack.ps1` releases it by default, `-Version` now optional.

## 2026-10-30

* **Initial version** (FOS-0001): resident WPF utility opened by a global shortcut (default Ctrl + Win) on a pre-built hidden window of tabs, columns and groups of folders and files; a click navigates the calling Explorer window or file dialog, else opens a new Explorer window or the file. Customize mode with drag and drop, 7 themes, 9 languages, first-run wizard, JSON configuration with backups, notification-area icon, Velopack installer and updates, `--self-test` and `--snapshot` modes.
* **Final review fixes** (FOS-0001): file-dialog text kept behind an unanswered OK; keyboard hook re-armed (resume, every 5 min) on a high-priority thread; shortcut recalls a Customize window left behind instead of hiding it; mapped network drives fail fast like UNC shares; wrapping tab bar; test zone tests only the applied shortcut; ready notification after the wizard; activation problems logged, critical exceptions let through; shell errors not doubled; navigation falls back to a new window; layout key names; flushed save, atomic export, tolerant backup names; log roll failure survived; HTTPS-only update feed; App test project.
