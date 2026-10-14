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

        Assert.True(System.IO.File.Exists(backupPath));
        Assert.Matches(@"urls_\d{8}_\d{6}\.txt$", Path.GetFileName(backupPath));
        Assert.Equal(new[] { "a.com", "b.com" }, System.IO.File.ReadAllLines(backupPath));
        Assert.Empty(store.Urls);
        Assert.Empty(System.IO.File.ReadAllLines(File));
    }
}
