# ClipWatcher

A small Windows desktop app that watches your clipboard and automatically saves any URLs it finds to a text file.

## Background & Purpose

As I populate my Links on my blog and gather items I may want to write up, I often have trouble keeping track of all the items I am interested in either posting or writing up.   And as I work through my local tools I figured a small app to test the "Vibe ability" of them might be useful.  So here it is - a single small project with two purposes.

First - something I can run on various computers, point at a network share and keep everything in one place.  Not a browser plugin which I'd need to keep synched across multiple computers and browsers.  Not a web interface where I'd have to go back and forth pasting and keeping all the browser security straight.  A simple .Net 10 app that I can use on my Windows box and something I could easily add a version for Linux if I need it since it's .Net 10 and runs everywhere.

Second - a test of the local environment.  Nothing cloud on this one, it was all using my local stack:
Visual Studio Code with OpenCode 1.18.35 on my desktop.
Lemonade Server 2026.41.1 on my Strix Halo LLM server with Qwen 3.8 Flash Next, testing using a halogen back end coordinated through the Lemonade server.
No code written by me, just using the agents in the OpenCode and the back end of the server.

I don't have a good Wall Clock time since I didn't catch every approve message immediately, but it ran for about an afternoon.
Project metrics were:
This session (WinForms clipboard URL logger, 99 assistant messages):
- Input: 2,998,713 · Output: 39,051 · Reasoning: 27,451 · Cache read: 5,171,479
- Total: ~8.24M tokens (~3.07M excluding cache reads)
Token cost $0 - the benefits of running local.  (After the costs of the Strix Halo are covered, of course.)

First test met all my stated requirements - a successful one-shot.

Feel free to fork, copy, re-use, or mangle as you wish.

## Features

- Monitors the clipboard continuously and extracts URLs (http/https, `www.`, and bare domains)
- Skips duplicates and ignores file links (e.g. `.pdf`, `.zip`, `.png`)
- Optional alphabetical sorting after each add (on by default, toggleable)
- Collapsible, editable view of the URL file with debounced auto-save
- **Backup & New** button: copies the current file to `<name>_<timestamp>.txt` and starts a fresh one
- Remembers your settings (file path, toggles) between runs

## Requirements

- Windows with .NET 10 SDK

## Build & Run

```
dotnet run --project ClipWatcher
```

Run tests with:

```
dotnet test
```

## How It Works

- URLs are appended to a plain-text file (default: `%AppData%\ClipWatcher\urls.txt`); use **Browse…** to point it anywhere
- The file is plain text, one URL per line — edit it freely; changes auto-save about a second after you stop typing
- Settings are stored in `%AppData%\ClipWatcher\settings.json`

## Project Structure

```
ClipWatcher/
  MainForm.cs            UI wiring
  Services/
    ClipboardMonitor.cs  Win32 clipboard listener
    UrlExtractor.cs      URL detection + blocklist
    UrlFileStore.cs      file model: load/save/dedupe/sort/backup
    AppSettings.cs       persisted settings
ClipWatcher.Tests/       xUnit tests for the services
```
