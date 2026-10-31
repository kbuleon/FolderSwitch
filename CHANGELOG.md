History of **software changes** to the project's programs. Changes that only create `docs` files are not tracked here.
This file is maintained by the agent. The log is timestamped and **concise**. It is not documentation.

------

## 2026-10-31

* **Version in the repository**: `<Version>` property in `Directory.Build.props` (0.1.1, the last release); `build/pack.ps1` releases it by default, `-Version` now optional.

## 2026-10-30

* **Initial version** (FOS-0001): resident WPF utility opened by a global shortcut (default Ctrl + Win) on a pre-built hidden window of tabs, columns and groups of folders and files; a click navigates the calling Explorer window or file dialog, else opens a new Explorer window or the file. Customize mode with drag and drop, 7 themes, 9 languages, first-run wizard, JSON configuration with backups, notification-area icon, Velopack installer and updates, `--self-test` and `--snapshot` modes.
* **Final review fixes** (FOS-0001): file-dialog text kept behind an unanswered OK; keyboard hook re-armed (resume, every 5 min) on a high-priority thread; shortcut recalls a Customize window left behind instead of hiding it; mapped network drives fail fast like UNC shares; wrapping tab bar; test zone tests only the applied shortcut; ready notification after the wizard; activation problems logged, critical exceptions let through; shell errors not doubled; navigation falls back to a new window; layout key names; flushed save, atomic export, tolerant backup names; log roll failure survived; HTTPS-only update feed; App test project.
