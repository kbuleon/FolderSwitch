# FolderSwitch.EmojiData

Makes the emoji data FolderSwitch ships for the icons of tabs and groups, from the Unicode files:

- `src/FolderSwitch.App/Emoji/catalog.json`: the emojis offered, in Unicode order, with their category;
- `src/FolderSwitch.App/Emoji/keywords/<code>.json`: for each of the nine languages, the name and the search keywords of every emoji of the catalog (CLDR annotations).

The application never fetches these files from the network: this tool is run by a maintainer, and its output is committed.

## Running it

```
dotnet run --project tools/FolderSwitch.EmojiData -- <working folder> [--sheet]
```

- `<working folder>` must be **outside the repository**. The tool downloads the Unicode files there (and reuses them on the next run); nothing downloaded is ever executed, the files are only parsed.
- `--sheet` also writes, into the working folder, one PNG contact sheet per category (`<category>-<page>.png`): every emoji of the catalog as WPF draws it in `Segoe UI Emoji` (one colour, twice the usual DPI) with its code point. This is what the application shows, so the sheets are how to judge a drawing.

The tool needs Windows (WPF, and the machine's `Segoe UI Emoji`).

## What it does

1. Reads `emoji-test.txt` of the Unicode Emoji version named by `UnicodeVersion` in `Program.cs`, and the CLDR annotations of the release tag named by `CldrRelease` (a tag of `unicode-org/cldr-json`, never a branch). Both are the latest published when the data was last generated; to update the data, change the two constants and run again.
2. Keeps the `fully-qualified` emojis that are **one code point**, optionally followed by U+FE0F: no flag, no skin tone, no ZWJ sequence, no keycap, no component. The Unicode groups give the categories and the order.
3. Removes the emojis listed in `excluded.txt` (one emoji per line, a comment after `#`).
4. Removes every emoji `Segoe UI Emoji` on this machine cannot draw: a code point missing from the font's glyph table, or a drawing with empty bounds. The output therefore depends on the font of the machine that runs the tool (a recent Windows 11); the application checks the font again at run time.
5. Writes the files. `pt-BR` comes from CLDR `pt`; `pt-PT` from CLDR `pt-PT`, which only says what differs from `pt`, so `pt` fills the rest.

## The hand list (`excluded.txt`)

WPF draws the font in one colour: many emojis lose what made them readable (colours, small details) at 16 px. After each change of version, run with `--sheet`, look at every sheet, and add to `excluded.txt` any emoji whose drawing is unreadable, confusing or ambiguous at that size. When in doubt, leave it out: the sort is made on Windows 11 and has to stand for Windows 10 too.
