using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
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
    private bool _pendingRefresh;
    private readonly List<string> _deferredAdds = new();
    private bool _expanded = true;
    private int _collapseDelta;

    public MainForm()
    {
        _settings = AppSettings.Load();
        _store = new UrlFileStore(_settings.FilePath) { SortAfterAdd = _settings.SortAfterAdd };

        Text = "ClipWatcher";
        using (var iconStream = typeof(MainForm).Assembly.GetManifestResourceStream("ClipWatcher.app.ico"))
        {
            if (iconStream is not null)
                Icon = new Icon(iconStream);
        }
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
            SaveSettings();
        };

        _sortCheck.Text = "Sort after add";
        _sortCheck.Location = new Point(160, 12);
        _sortCheck.AutoSize = true;
        _sortCheck.Checked = _settings.SortAfterAdd;
        _sortCheck.CheckedChanged += (_, _) =>
        {
            _store.SortAfterAdd = _sortCheck.Checked;
            _settings.SortAfterAdd = _sortCheck.Checked;
            SaveSettings();
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
        _contentBox.Leave += (_, _) =>
        {
            if (_pendingRefresh)
            {
                _pendingRefresh = false;
                FlushEdits();
                RefreshView();
            }
        };

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
        if (!_monitor.Attach(Handle))
        {
            _monitorCheck.Checked = false;
            SetStatus("Clipboard monitoring unavailable (listener registration failed)");
        }
        _monitor.ClipboardUpdated += OnClipboardUpdated;
        try
        {
            _store.Load();
        }
        catch (Exception ex)
        {
            SetStatus($"Could not read file: {ex.Message}");
        }
        RefreshView();
        if (string.IsNullOrEmpty(_statusLabel.Text)) SetStatus("Ready");
    }

    private void OnClipboardUpdated()
    {
        string text;
        try { text = Clipboard.GetText(); }
        catch (Exception ex) when (ex is ExternalException or COMException or OutOfMemoryException) { return; }

        IReadOnlyList<string> candidates;
        try { candidates = UrlExtractor.Extract(text); }
        catch (RegexMatchTimeoutException) { return; }
        if (candidates.Count == 0) return;

        _store.SetContents(_contentBox.Lines);
        if (_deferredAdds.Count > 0)
            _store.AddRange(_deferredAdds);
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
        if (_contentBox.Focused)
        {
            _pendingRefresh = true;
            _deferredAdds.AddRange(candidates);
        }
        else
        {
            RefreshView();
        }
        SetStatus($"Added {added} URL(s) · saved {DateTime.Now:h:mm tt}");
    }

    private void OnContentChanged(object? sender, EventArgs e)
    {
        if (_suppressEditEvents) return;
        _pendingSave = true;
        _debounce.Stop();
        _debounce.Start();
    }

    private bool FlushEdits()
    {
        if (!_pendingSave) return true;
        _pendingSave = false;
        _store.SetContents(_contentBox.Lines);
        if (_deferredAdds.Count > 0)
            _store.AddRange(_deferredAdds);
        UpdateCountLabel();
        try
        {
            _store.Save();
            _deferredAdds.Clear();
            SetStatus($"Saved {DateTime.Now:h:mm tt}");
            return true;
        }
        catch (Exception ex)
        {
            _pendingSave = true;
            SetStatus($"Save failed: {ex.Message}");
            return false;
        }
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

        if (!FlushEdits()) return;
        _store.FilePath = dialog.FileName;
        _settings.FilePath = dialog.FileName;
        SaveSettings();
        _pathBox.Text = _store.FilePath;
        var loadFailed = false;
        try
        {
            _store.Load();
        }
        catch (Exception ex)
        {
            loadFailed = true;
            SetStatus($"Could not read file: {ex.Message}");
        }
        RefreshView();
        if (!loadFailed) SetStatus($"Now tracking {Path.GetFileName(_store.FilePath)}");
    }

    private void OnBackup(object? sender, EventArgs e)
    {
        var answer = MessageBox.Show(this,
            "Back up the current file and start a new empty one?",
            "Backup & New", MessageBoxButtons.OKCancel, MessageBoxIcon.Question);
        if (answer != DialogResult.OK) return;

        _debounce.Stop();
        if (!FlushEdits()) return;
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
        _deferredAdds.Clear();
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

    private void SaveSettings()
    {
        try
        {
            _settings.Save();
        }
        catch (Exception ex)
        {
            SetStatus($"Could not save settings: {ex.Message}");
        }
    }

    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        _debounce.Stop();
        _debounce.Dispose();
        FlushEdits();
        _monitor.ClipboardUpdated -= OnClipboardUpdated;
        _monitor.Detach();
        SaveSettings();
    }
}
