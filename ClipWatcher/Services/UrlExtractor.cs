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
