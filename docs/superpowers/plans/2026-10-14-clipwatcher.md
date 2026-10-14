# ClipWatcher Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A WinForms (.NET 10) app that watches the clipboard, extracts URLs, and appends them to a configurable .txt file with optional alphabetical sorting, an editable/collapsible file view, and timestamped backup-and-reset.

**Architecture:** Light layering — a thin `MainForm` wires four focused services: `ClipboardMonitor` (Win32 clipboard listener), `UrlExtractor` (regex), `UrlFileStore` (file model), `AppSettings` (JSON persistence). Services are UI-free and unit-tested; the form owns all interaction.

**Tech Stack:** .NET 10 (`net10.0-windows`), WinForms, System.Text.Json, xunit. Spec: `docs/superpowers/specs/2026-10-14-clipwatcher-design.md`.

---

### Task 1: Scaffold solution and projects

**Files:**
- Create: `ClipWatcher.sln`, `ClipWatcher/ClipWatcher.csproj`, `ClipWatcher.Tests/ClipWatcher.Tests.csproj`

- [ ] **Step 1: Init git (repo does not exist yet)**

```powershell
git init
```

- [ ] **Step 2: Create solution and projects**

```powershell
dotnet new sln -n ClipWatcher
dotnet new winforms -n ClipWatcher -o ClipWatcher -f net10.0
dotnet new xunit -n ClipWatcher.Tests -o ClipWatcher.Tests -f net10.0
dotnet sln add ClipWatcher ClipWatcher.Tests
dotnet add ClipWatcher.Tests reference ClipWatcher
```

- [ ] **Step 3: Delete template files we will replace**

```powershell
Remove-Item ClipWatcher/Form1.cs, ClipWatcher/Form1.Designer.cs, ClipWatcher/Form1.resx -ErrorAction SilentlyContinue
Remove-Item ClipWatcher.Tests/UnitTest1.cs -ErrorAction SilentlyContinue
```

- [ ] **Step 4: Build to verify scaffold**

Run: `dotnet build`
Expected: `Build succeeded`, 0 errors. (Template `Program.cs` references `Form1`; if the build fails on that, temporarily restore `Form1` files or proceed — Task 6 rewrites `Program.cs`. If it fails, re-run Step 3 skipping Form1 deletion and delete only in Task 6.)

- [ ] **Step 5: Commit**

```powershell
git add -A
git commit -m "chore: scaffold ClipWatcher WinForms + xunit projects"
```

---

### Task 2: UrlExtractor (TDD)

**Files:**
- Create: `ClipWatcher/Services/UrlExtractor.cs`
- Test: `ClipWatcher.Tests/UrlExtractorTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `ClipWatcher.Tests/UrlExtractorTests.cs`:

```csharp
using ClipWatcher.Services;
using Xunit;

namespace ClipWatcher.Tests;

public class UrlExtractorTests
{
    [Fact]
    public void Extract_FindsSchemeUrls()
    {
        var result = UrlExtractor.Extract("check out https://example.com/page?a=1 and http://foo.bar/x");
        Assert.Equal(new[] { "https://example.com/page?a=1", "http://foo.bar/x" }, result);
    }

    [Fact]
    public void Extract_FindsWwwUrl()
    {
        Assert.Equal(new[] { "www.example.org" }, UrlExtractor.Extract("visit www.example.org now"));
    }

    [Fact]
    public void Extract_FindsBareDomainWithPath()
    {
        Assert.Equal(new[] { "example.com/path" }, UrlExtractor.Extract("bare example.com/path works"));
    }

    [Fact]
    public void Extract_FindsDomainWithPort()
    {
        Assert.Equal(new[] { "example.com:8080/x" }, UrlExtractor.Extract("port example.com:8080/x here"));
    }

    [Fact]
    public void Extract_TrimsTrailingPunctuation()
    {
        Assert.Equal(new[] { "https://example.com" }, UrlExtractor.Extract("See https://example.com."));
    }

    [Fact]
    public void Extract_IgnoresFileExtensions()
    {
        Assert.Empty(UrlExtractor.Extract("open notes.pdf please"));
        Assert.Empty(UrlExtractor.Extract("file is report.docx"));
    }

    [Fact]
    public void Extract_IgnoresPlainText()
    {
        Assert.Empty(UrlExtractor.Extract("hello world one two"));
        Assert.Empty(UrlExtractor.Extract("version 1.2.3"));
    }

    [Fact]
    public void Extract_FindsMultipleUrlsInOnePayload()
    {
        var result = UrlExtractor.Extract("https://a.co/b. Also www.b.co and c.org?q=1");
        Assert.Equal(new[] { "https://a.co/b", "www.b.co", "c.org?q=1" }, result);
    }

    [Fact]
    public void Extract_DeduplicatesWithinPayload()
    {
        var result = UrlExtractor.Extract("example.com and EXAMPLE.com");
        Assert.Single(result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Extract_HandlesEmptyInput(string? text)
    {
        Assert.Empty(UrlExtractor.Extract(text));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter UrlExtractorTests`
Expected: build error `The type or namespace name 'UrlExtractor' does not exist` (compile failure counts as fail).

- [ ] **Step 3: Write minimal implementation**

Create `ClipWatcher/Services/UrlExtractor.cs`:

```csharp
using System.Text.RegularExpressions;

namespace ClipWatcher.Services;

public static class UrlExtractor
{
    private const string UrlPattern =
        @"https?://[^\s""'<>]+|www\.[^\s""'<>]+|" +
        @"[a-zA-Z0-9](?:[a-zA-Z0-9-]*[a-zA-Z0-9])?(?:\.[a-zA-Z0-9](?:[a-zA-Z0-9-]*[a-zA-Z0-9])?)*\.[a-zA-Z]{2,}(?::\d{1,5})?(?:[/?#][^\s""'<>]*)?";

    private static readonly HashSet<string> BlockedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        "doc", "docx", "xls", "xlsx", "ppt", "pptx", "pdf", "png", "jpg", "jpeg", "gif", "bmp",
        "svg", "webp", "ico", "zip", "rar", "7z", "tar", "gz", "exe", "msi", "dll", "cs",
        "csproj", "sln", "md", "txt", "json", "xml", "html", "htm", "css", "js", "ts", "py",
        "mp3", "mp4", "avi", "mov", "csv", "iso", "bin", "log", "ini", "cfg", "yaml", "yml"
    };

    public static IReadOnlyList<string> Extract(string? text)
    {
        var results = new List<string>();
        if (string.IsNullOrWhiteSpace(text)) return results;

        foreach (Match match in Regex.Matches(text, UrlPattern, RegexOptions.IgnoreCase))
        {
            var url = match.Value.TrimEnd('.', ',', ';', ':', '!', '?', ')', ']', '}', '"', '\'');
            if (url.Length == 0) continue;
            if (IsBlocked(url)) continue;
            if (!results.Contains(url, StringComparer.OrdinalIgnoreCase)) results.Add(url);
        }
        return results;
    }

    private static bool IsBlocked(string url)
    {
        var host = url;
        var schemeIndex = host.IndexOf("://", StringComparison.Ordinal);
        if (schemeIndex >= 0) host = host[(schemeIndex + 3)..];
        var cut = host.IndexOfAny(['/', '?', '#']);
        if (cut >= 0) host = host[..cut];
        var portIndex = host.LastIndexOf(':');
        if (portIndex >= 0) host = host[..portIndex];
        var lastDot = host.LastIndexOf('.');
        if (lastDot < 0 || lastDot == host.Length - 1) return false;
        return BlockedExtensions.Contains(host[(lastDot + 1)..]);
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test --filter UrlExtractorTests`
Expected: `Passed! - Failed: 0, Passed: 13`

- [ ] **Step 5: Commit**

```powershell
git add ClipWatcher/Services/UrlExtractor.cs ClipWatcher.Tests/UrlExtractorTests.cs
git commit -m "feat: URL extraction with bare-domain matching and extension blocklist"
```

---

### Task 3: UrlFileStore (TDD)

**Files:**
- Create: `ClipWatcher/Services/UrlFileStore.cs`
- Test: `ClipWatcher.Tests/UrlFileStoreTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `ClipWatcher.Tests/UrlFileStoreTests.cs`:

```csharp
using ClipWatcher.Services;
using Xunit;

namespace ClipWatcher.Tests;

public class UrlFileStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "clipwatcher-tests-" + Guid.NewGuid().ToString("N"));
    private string File => Path.Combine(_dir, "urls.txt");

    public UrlFileStoreTests() => Directory.CreateDirectory(_dir);
    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void Load_MissingFile_YieldsEmpty()
    {
        var store = new UrlFileStore(File);
        store.Load();
        Assert.Empty(store.Urls);
    }

    [Fact]
    public void SaveLoad_RoundTrips()
    {
        var store = new UrlFileStore(File);
        store.AddRange(new[] { "https://a.com", "b.org" });
        store.Save();

        var reloaded = new UrlFileStore(File);
        reloaded.Load();
        Assert.Equal(new[] { "https://a.com", "b.org" }, reloaded.Urls);
    }

    [Fact]
    public void AddRange_SkipsDuplicatesCaseInsensitive()
    {
        var store = new UrlFileStore(File);
        Assert.Equal(2, store.AddRange(new[] { "a.com", "B.com" }));
        Assert.Equal(0, store.AddRange(new[] { "A.COM", "b.com" }));
        Assert.Equal(2, store.Urls.Count);
    }

    [Fact]
    public void AddRange_SortsWhenEnabled()
    {
        var store = new UrlFileStore(File) { SortAfterAdd = true };
        store.AddRange(new[] { "zebra.com", "apple.com" });
        Assert.Equal(new[] { "apple.com", "zebra.com" }, store.Urls);
    }

    [Fact]
    public void AddRange_AppendsWhenSortDisabled()
    {
        var store = new UrlFileStore(File) { SortAfterAdd = false };
        store.AddRange(new[] { "zebra.com", "apple.com" });
        Assert.Equal(new[] { "zebra.com", "apple.com" }, store.Urls);
    }

    [Fact]
    public void SetContents_ReplacesListFromEditedLines()
    {
        var store = new UrlFileStore(File);
        store.AddRange(new[] { "a.com" });
        store.SetContents(new[] { "x.com", "", "  y.com  " });
        Assert.Equal(new[] { "x.com", "y.com" }, store.Urls);
    }

    [Fact]
    public void Backup_CopiesTimestampedAndClearsWorkingFile()
    {
        var store = new UrlFileStore(File);
        store.AddRange(new[] { "a.com", "b.com" });
        store.Save();

        var backupPath = store.Backup();

        Assert.True(File.Exists(backupPath));
        Assert.Matches(@"urls_\d{8}_\d{6}\.txt$", Path.GetFileName(backupPath));
        Assert.Equal(new[] { "a.com", "b.com" }, File.ReadAllLines(backupPath));
        Assert.Empty(store.Urls);
        Assert.Empty(File.ReadAllLines(File));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter UrlFileStoreTests`
Expected: compile failure — `UrlFileStore` does not exist.

- [ ] **Step 3: Write minimal implementation**

Create `ClipWatcher/Services/UrlFileStore.cs`:

```csharp
namespace ClipWatcher.Services;

public sealed class UrlFileStore
{
    private readonly List<string> _urls = new();

    public UrlFileStore(string filePath) => FilePath = filePath;

    public string FilePath { get; set; }
    public bool SortAfterAdd { get; set; } = true;
    public IReadOnlyList<string> Urls => _urls;

    public void Load()
    {
        _urls.Clear();
        if (!File.Exists(FilePath)) return;
        foreach (var line in File.ReadAllLines(FilePath))
        {
            var trimmed = line.Trim();
            if (trimmed.Length > 0 && !_urls.Contains(trimmed, StringComparer.OrdinalIgnoreCase))
                _urls.Add(trimmed);
        }
    }

    public int AddRange(IEnumerable<string> candidates)
    {
        var added = 0;
        foreach (var url in candidates)
        {
            if (_urls.Contains(url, StringComparer.OrdinalIgnoreCase)) continue;
            _urls.Add(url);
            added++;
        }
        if (added > 0 && SortAfterAdd)
            _urls.Sort(StringComparer.OrdinalIgnoreCase);
        return added;
    }

    public void SetContents(IEnumerable<string> lines)
    {
        _urls.Clear();
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.Length > 0) _urls.Add(trimmed);
        }
    }

    public void Save()
    {
        var dir = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.WriteAllLines(FilePath, _urls);
    }

    public string Backup()
    {
        var dir = Path.GetDirectoryName(FilePath) ?? ".";
        var name = $"{Path.GetFileNameWithoutExtension(FilePath)}_{DateTime.Now:yyyyMMdd_HHmmss}{Path.GetExtension(FilePath)}";
        var backupPath = Path.Combine(dir, name);
        if (File.Exists(FilePath)) File.Copy(FilePath, backupPath);
        else File.WriteAllText(backupPath, string.Empty);
        _urls.Clear();
        File.WriteAllLines(FilePath, Array.Empty<string>());
        return backupPath;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test --filter UrlFileStoreTests`
Expected: `Passed! - Failed: 0, Passed: 7`

- [ ] **Step 5: Commit**

```powershell
git add ClipWatcher/Services/UrlFileStore.cs ClipWatcher.Tests/UrlFileStoreTests.cs
git commit -m "feat: URL file store with dedupe, sort toggle, and timestamped backup"
```

---

### Task 4: AppSettings

**Files:**
- Create: `ClipWatcher/Services/AppSettings.cs`

No unit tests (trivial JSON persistence; covered by smoke test in Task 7).

- [ ] **Step 1: Write implementation**

Create `ClipWatcher/Services/AppSettings.cs`:

```csharp
using System.Text.Json;

namespace ClipWatcher.Services;

public sealed class AppSettings
{
    public string FilePath { get; set; } = Path.Combine(SettingsDir, "urls.txt");
    public bool SortAfterAdd { get; set; } = true;
    public bool MonitoringEnabled { get; set; } = true;

    private static string SettingsDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ClipWatcher");

    private static string SettingsPath => Path.Combine(SettingsDir, "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var loaded = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath));
                if (loaded is not null) return loaded;
            }
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
        }
        return new AppSettings();
    }

    public void Save()
    {
        Directory.CreateDirectory(SettingsDir);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }
}
```

- [ ] **Step 2: Build**

Run: `dotnet build`
Expected: `Build succeeded`.

- [ ] **Step 3: Commit**

```powershell
git add ClipWatcher/Services/AppSettings.cs
git commit -m "feat: JSON app settings persisted to %AppData%\\ClipWatcher"
```

---

### Task 5: ClipboardMonitor

**Files:**
- Create: `ClipWatcher/Services/ClipboardMonitor.cs`

Not unit-testable (Win32 message plumbing); verified by smoke test in Task 7.

- [ ] **Step 1: Write implementation**

Create `ClipWatcher/Services/ClipboardMonitor.cs`:

```csharp
using System.Runtime.InteropServices;

namespace ClipWatcher.Services;

public sealed class ClipboardMonitor : IDisposable
{
    private const int WM_CLIPBOARDUPDATE = 0x031D;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool AddClipboardFormatListener(IntPtr hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

    public bool IsEnabled { get; set; } = true;
    public event Action? ClipboardUpdated;

    private IntPtr _hwnd;
    private bool _registered;

    public void Attach(IntPtr hwnd)
    {
        _hwnd = hwnd;
        _registered = AddClipboardFormatListener(hwnd);
    }

    public void Detach()
    {
        if (!_registered) return;
        RemoveClipboardFormatListener(_hwnd);
        _registered = false;
    }

    public void WndProc(ref Message m)
    {
        if (m.Msg == WM_CLIPBOARDUPDATE && IsEnabled)
            ClipboardUpdated?.Invoke();
    }

    public void Dispose() => Detach();
}
```

- [ ] **Step 2: Build**

Run: `dotnet build`
Expected: `Build succeeded`.

- [ ] **Step 3: Commit**

```powershell
git add ClipWatcher/Services/ClipboardMonitor.cs
git commit -m "feat: event-driven clipboard monitor via AddClipboardFormatListener"
```

---

### Task 6: MainForm + Program wiring

**Files:**
- Create: `ClipWatcher/MainForm.cs`
- Modify: `ClipWatcher/Program.cs` (full rewrite)

- [ ] **Step 1: Write MainForm**

Create `ClipWatcher/MainForm.cs`:

```csharp
using ClipWatcher.Services;

namespace ClipWatcher;

public sealed class MainForm : Form
{
    private readonly AppSettings _settings;
    private readonly UrlFileStore _store;
    private readonly ClipboardMonitor _monitor = new();
    private readonly System.Windows.Forms.Timer _debounce = new() { Interval = 1000 };

    private readonly CheckBox _monitorCheck = new();
    private readonly CheckBox _sortCheck = new();
    private readonly TextBox _pathBox = new();
    private readonly Button _browseBtn = new();
    private readonly Button _backupBtn = new();
    private readonly Button _toggleBtn = new();
    private readonly TextBox _contentBox = new();
    private readonly StatusStrip _statusStrip = new();
    private readonly ToolStripStatusLabel _statusLabel = new();

    private bool _suppressEditEvents;
    private bool _pendingSave;
    private bool _expanded = true;
    private int _collapseDelta;

    public MainForm()
    {
        _settings = AppSettings.Load();
        _store = new UrlFileStore(_settings.FilePath) { SortAfterAdd = _settings.SortAfterAdd };

        Text = "ClipWatcher";
        Width = 660;
        Height = 520;
        MinimumSize = new Size(480, 240);
        StartPosition = FormStartPosition.CenterScreen;

        _monitorCheck.Text = "Monitor clipboard";
        _monitorCheck.Location = new Point(12, 12);
        _monitorCheck.AutoSize = true;
        _monitorCheck.Checked = _settings.MonitoringEnabled;
        _monitorCheck.CheckedChanged += (_, _) =>
        {
            _monitor.IsEnabled = _monitorCheck.Checked;
            _settings.MonitoringEnabled = _monitorCheck.Checked;
            _settings.Save();
        };

        _sortCheck.Text = "Sort after add";
        _sortCheck.Location = new Point(160, 12);
        _sortCheck.AutoSize = true;
        _sortCheck.Checked = _settings.SortAfterAdd;
        _sortCheck.CheckedChanged += (_, _) =>
        {
            _store.SortAfterAdd = _sortCheck.Checked;
            _settings.SortAfterAdd = _sortCheck.Checked;
            _settings.Save();
        };

        _pathBox.Location = new Point(12, 40);
        _pathBox.Width = 440;
        _pathBox.ReadOnly = true;
        _pathBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _pathBox.Text = _store.FilePath;

        _browseBtn.Text = "Browse…";
        _browseBtn.Location = new Point(458, 39);
        _browseBtn.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _browseBtn.Click += OnBrowse;

        _backupBtn.Text = "Backup & New";
        _backupBtn.Location = new Point(552, 39);
        _backupBtn.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _backupBtn.Click += OnBackup;

        _toggleBtn.Text = "▼ File contents (0)";
        _toggleBtn.Location = new Point(12, 72);
        _toggleBtn.AutoSize = true;
        _toggleBtn.FlatStyle = FlatStyle.Flat;
        _toggleBtn.FlatAppearance.BorderSize = 0;
        _toggleBtn.Cursor = Cursors.Hand;
        _toggleBtn.Click += OnToggleContents;

        _contentBox.Location = new Point(12, 100);
        _contentBox.Size = new Size(620, 340);
        _contentBox.Multiline = true;
        _contentBox.ScrollBars = ScrollBars.Vertical;
        _contentBox.Font = new Font("Consolas", 9.75f);
        _contentBox.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        _contentBox.TextChanged += OnContentChanged;

        _statusStrip.Items.Add(_statusLabel);

        Controls.AddRange(new Control[]
        {
            _monitorCheck, _sortCheck, _pathBox, _browseBtn, _backupBtn,
            _toggleBtn, _contentBox, _statusStrip
        });

        _debounce.Tick += (_, _) =>
        {
            _debounce.Stop();
            FlushEdits();
        };

        Load += OnLoad;
        FormClosing += OnFormClosing;
    }

    protected override void WndProc(ref Message m)
    {
        _monitor.WndProc(ref m);
        base.WndProc(ref m);
    }

    private void OnLoad(object? sender, EventArgs e)
    {
        _monitor.Attach(Handle);
        _monitor.ClipboardUpdated += OnClipboardUpdated;
        _store.Load();
        RefreshView();
        SetStatus("Ready");
    }

    private void OnClipboardUpdated()
    {
        string text;
        try { text = Clipboard.GetText(); }
        catch (ExternalException) { return; }

        var candidates = UrlExtractor.Extract(text);
        if (candidates.Count == 0) return;

        _store.SetContents(_contentBox.Lines);
        var added = _store.AddRange(candidates);
        if (added == 0) return;

        try
        {
            _store.Save();
            _pendingSave = false;
        }
        catch (Exception ex)
        {
            SetStatus($"Save failed: {ex.Message}");
            return;
        }
        RefreshView();
        SetStatus($"Added {added} URL(s) · saved {DateTime.Now:h:mm tt}");
    }

    private void OnContentChanged(object? sender, EventArgs e)
    {
        if (_suppressEditEvents) return;
        _pendingSave = true;
        _debounce.Stop();
        _debounce.Start();
    }

    private void FlushEdits()
    {
        if (!_pendingSave) return;
        _pendingSave = false;
        _store.SetContents(_contentBox.Lines);
        try
        {
            _store.Save();
            SetStatus($"Saved {DateTime.Now:h:mm tt}");
        }
        catch (Exception ex)
        {
            _pendingSave = true;
            SetStatus($"Save failed: {ex.Message}");
        }
        UpdateCountLabel();
    }

    private void OnBrowse(object? sender, EventArgs e)
    {
        using var dialog = new SaveFileDialog
        {
            Title = "Choose URL file",
            Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
            FileName = Path.GetFileName(_store.FilePath)
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        FlushEdits();
        _store.FilePath = dialog.FileName;
        _settings.FilePath = dialog.FileName;
        _settings.Save();
        _pathBox.Text = _store.FilePath;
        _store.Load();
        RefreshView();
        SetStatus($"Now tracking {Path.GetFileName(_store.FilePath)}");
    }

    private void OnBackup(object? sender, EventArgs e)
    {
        var answer = MessageBox.Show(this,
            "Back up the current file and start a new empty one?",
            "Backup & New", MessageBoxButtons.OKCancel, MessageBoxIcon.Question);
        if (answer != DialogResult.OK) return;

        _debounce.Stop();
        _pendingSave = false;
        try
        {
            var backupPath = _store.Backup();
            RefreshView();
            SetStatus($"Backed up to {Path.GetFileName(backupPath)}");
        }
        catch (Exception ex)
        {
            SetStatus($"Backup failed: {ex.Message}");
        }
    }

    private void OnToggleContents(object? sender, EventArgs e)
    {
        _expanded = !_expanded;
        if (_expanded)
        {
            _contentBox.Visible = true;
            Height += _collapseDelta;
        }
        else
        {
            _collapseDelta = _contentBox.Height;
            _contentBox.Visible = false;
            Height -= _collapseDelta;
        }
        UpdateCountLabel();
    }

    private void RefreshView()
    {
        _suppressEditEvents = true;
        _contentBox.Text = string.Join(Environment.NewLine, _store.Urls);
        _suppressEditEvents = false;
        UpdateCountLabel();
    }

    private void UpdateCountLabel()
    {
        var arrow = _expanded ? "▼" : "▶";
        _toggleBtn.Text = $"{arrow} File contents ({_store.Urls.Count})";
    }

    private void SetStatus(string message) => _statusLabel.Text = message;

    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        _debounce.Stop();
        FlushEdits();
        _monitor.ClipboardUpdated -= OnClipboardUpdated;
        _monitor.Detach();
        _settings.Save();
    }
}
```

- [ ] **Step 2: Rewrite Program.cs**

Replace `ClipWatcher/Program.cs` entirely:

```csharp
namespace ClipWatcher;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}
```

- [ ] **Step 3: Build**

Run: `dotnet build`
Expected: `Build succeeded`, 0 errors.

- [ ] **Step 4: Commit**

```powershell
git add ClipWatcher/MainForm.cs ClipWatcher/Program.cs
git commit -m "feat: MainForm wiring clipboard, file view, backup, and settings"
```

---

### Task 7: Smoke test and final verification

**Files:** none new

- [ ] **Step 1: Run full test suite**

Run: `dotnet test`
Expected: `Passed! - Failed: 0, Passed: 20` (13 extractor + 7 store).

- [ ] **Step 2: Launch the app**

Run: `dotnet run --project ClipWatcher` (leave running; interact manually).

- [ ] **Step 3: Manual smoke checklist**

1. Copy `https://example.com/one` → status shows "Added 1 URL(s)", textbox shows it, `%AppData%\ClipWatcher\urls.txt` contains it.
2. Copy the same URL again → no change (dedupe).
3. Copy `notes.pdf` → nothing added (blocklist).
4. Uncheck "Sort after add", copy `aaa.com` → appended at bottom, not sorted.
5. Re-check sort, copy `zzz.com` then `mmm.com` → list re-sorts alphabetically.
6. Edit textbox (add a line, delete a line) → ~1s later status shows "Saved", file matches.
7. Click ▼ → contents collapse, form shrinks; click ▶ → restores.
8. Click "Backup & New" → confirm → `urls_<timestamp>.txt` appears beside file, working file empty, view shows (0).
9. Close app, reopen → file path, sort toggle, monitor toggle, and file contents persist.

- [ ] **Step 4: Final commit**

```powershell
git add -A
git commit -m "docs: ClipWatcher design spec and implementation plan"
```

---

## Self-review notes

- Spec coverage: every spec section maps to a task (extractor→2, store→3, settings→4, monitor→5, UI→6, error handling in 6, tests in 2/3, smoke in 7).
- Type consistency: `Extract`, `AddRange`, `SetContents`, `Save`, `Backup`, `Load`, `Attach`, `Detach`, `WndProc`, `IsEnabled`, `SortAfterAdd`, `Urls`, `FilePath` used identically across tasks.
- No placeholders remain.
