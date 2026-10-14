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
