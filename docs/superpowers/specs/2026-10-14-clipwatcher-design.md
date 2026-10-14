# ClipWatcher — Design Spec

Date: 2026-10-14
Status: Approved by user

## Purpose

A small WinForms app (.NET 10) that monitors the Windows clipboard, extracts URLs from
clipboard text, and appends them to a configurable `.txt` file (one URL per line).

## Confirmed decisions

| Topic | Decision |
|---|---|
| URL matching | Any domain-like text: `http(s)://…`, `www.…`, bare domains (`example.com/path`) |
| False positives | Blocklist of common file extensions (`.docx`, `.png`, `.pdf`, `.cs`, …) rejects filename matches |
| Duplicates | Skipped (case-insensitive full-string comparison) |
| Edit saving | Auto-save, debounced ~1s after typing stops, plus on form close |
| Backup | Copy working file to `<name>_<yyyyMMdd_HHmmss>.txt` beside it, then clear working file to empty |
| Settings persistence | JSON in `%AppData%\ClipWatcher\settings.json` |
| Sorting | Case-insensitive ordinal; applied after each add (toggleable off → append to bottom) |
| Architecture | Option B: light layering, no system tray |

## Project structure

```
ClipWatcher.sln
ClipWatcher/
  ClipWatcher.csproj        # net10.0-windows, UseWindowsForms
  Program.cs
  MainForm.cs               # thin UI: controls + wiring only
  Services/
    ClipboardMonitor.cs     # Win32 AddClipboardFormatListener / WM_CLIPBOARDUPDATE
    UrlExtractor.cs         # regex extraction + normalization
    UrlFileStore.cs         # load/append/dedupe/sort/save/backup
    AppSettings.cs          # JSON persistence in %AppData%\ClipWatcher
ClipWatcher.Tests/
  ClipWatcher.Tests.csproj  # xunit
  UrlExtractorTests.cs
  UrlFileStoreTests.cs
```

## Components

### ClipboardMonitor
- P/Invoke `AddClipboardFormatListener` against the main form's window handle.
- `WndProc` override in `MainForm` forwards `WM_CLIPBOARDUPDATE` (0x031D) to the monitor.
- Raises `ClipboardTextChanged` only when `IsEnabled` is true (the UI on/off toggle).
- Event-driven; no polling timer.

### UrlExtractor
- Static `Extract(string text) -> IReadOnlyList<string>`.
- Matches, in order: `https?://\S+`, `www\.\S+`, bare domain
  `[label.]*[label.]*tld(:port)?([/?#]\S*)?` with TLD of 2+ letters.
- Rejects a match when the host's top-level label is a blocked file extension
  (case-insensitive blocklist: docx, xlsx, pptx, pdf, png, jpg, jpeg, gif,
  bmp, svg, webp, zip, rar, 7z, tar, gz, exe, dll, cs, csproj, sln, md, txt, json,
  xml, html, htm, css, js, ts, py, and similar).
- Trims trailing punctuation `. , ; : ! ? ) ] } " '` from each match.
- Returns all matches found in one clipboard payload, in order of appearance.

### UrlFileStore
- Holds the working list of URLs; `Load()` reads the file (missing file → empty list).
- `AddRange(urls)`: appends only URLs not already present (case-insensitive);
  when `SortAfterAdd` is set, sorts the whole list with `StringComparer.OrdinalIgnoreCase`.
- `Save()` writes one URL per line (UTF-8, no BOM).
- `Backup()`: copies current file to `<stem>_<yyyyMMdd_HHmmss>.txt` in the same
  directory, then clears the working file and in-memory list.
- IO errors propagate as `IOException`/`UnauthorizedAccessException` to the UI layer.

### AppSettings
- Properties: `FilePath`, `SortAfterAdd` (default true), `MonitoringEnabled` (default true).
- `Load()` / `Save()` to `%AppData%\ClipWatcher\settings.json` (System.Text.Json).
- Corrupt or missing file → defaults, file rewritten on next change.

## MainForm

```
[✓] Monitor clipboard    [✓] Sort after add
File: [ C:\...\urls.txt          ] [Browse…]  [Backup & New]
▼ File contents (12)
┌──────────────────────────────────────┐
│ multiline editable TextBox, monospace│
└──────────────────────────────────────┘
Status bar: "Added 2 URLs · saved 3:42 PM"
```

- Contents panel collapses/expands via the ▼/▶ toggle button; count shown in label.
- TextBox is editable; `TextChanged` restarts a 1-second debounce timer → `Save()`.
- Sort is applied on adds only, never while the user is typing.
- If a URL arrives while the user is editing, it is appended to the current TextBox
  content (and sorted if the toggle is on).
- Browse: `SaveFileDialog` to pick/create the URL file.
- Backup & New: confirm dialog, then `Backup()`, refresh view, status message.
- Form close: flush pending debounced save, remove clipboard listener.

## Clipboard flow

1. `WM_CLIPBOARDUPDATE` → `ClipboardMonitor` (if enabled) → `MainForm` handler.
2. Read `Clipboard.GetText()`; empty/non-text → ignore.
3. `UrlExtractor.Extract(text)` → candidate URLs.
4. `UrlFileStore.AddRange(candidates)` → returns count of newly added.
5. If count > 0: `Save()`, refresh TextBox + count label, status bar message.

## Error handling

- File locked / IO error on save → status bar warning; in-memory list kept; retried
  on next save trigger.
- Missing file → created on first write.
- Non-text clipboard content → ignored silently.
- Settings unreadable → defaults used.

## Testing (xunit)

- UrlExtractor: scheme URLs, www URLs, bare domains, ports/paths/queries, multiple
  URLs in one payload, trailing punctuation trimming, extension blocklist,
  near-miss text (e.g. "hello world", "one two") yields nothing.
- UrlFileStore: dedupe case-insensitive, sort-on-add vs append-to-bottom, backup
  file naming + working file cleared, missing file loads empty, save/load round-trip.

## Out of scope (YAGNI)

System tray, start-with-Windows, URL visiting/opening, history of clipboard text,
multiple files, plugin system.
