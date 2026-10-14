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
        foreach (var candidate in candidates)
        {
            var url = candidate.Trim();
            if (url.Length == 0 || _urls.Contains(url, StringComparer.OrdinalIgnoreCase)) continue;
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
