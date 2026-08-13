using System.Net;
using System.Text;
using FluentAssertions;
using PrimeOSTuner.Core.Updates;
using Xunit;

namespace PrimeOSTuner.Tests.Updates;

public class UpdateCheckerTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _body;
        public StubHandler(string body, HttpStatusCode status = HttpStatusCode.OK)
        {
            _body = body;
            _status = status;
        }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct)
            => Task.FromResult(new HttpResponseMessage(_status)
            {
                Content = new StringContent(_body, Encoding.UTF8, "application/json")
            });
    }

    private static string Release(string tag, bool draft = false, bool prerelease = false,
        string assetName = "crustcut-v9.9.9-win-x64.zip") => $$"""
        {
          "tag_name": "{{tag}}",
          "draft": {{draft.ToString().ToLowerInvariant()}},
          "prerelease": {{prerelease.ToString().ToLowerInvariant()}},
          "body": "notes here",
          "html_url": "https://example.invalid/release",
          "assets": [
            { "name": "{{assetName}}", "browser_download_url": "https://example.invalid/{{assetName}}" }
          ]
        }
        """;

    private static UpdateChecker Checker(string body, HttpStatusCode status = HttpStatusCode.OK)
        => new(new HttpClient(new StubHandler(body, status)), assetKeyword: "win");

    [Fact]
    public async Task Reports_a_newer_release()
    {
        var update = await Checker(Release("v9.9.9")).CheckAsync(new Version(0, 9, 3));

        update.Should().NotBeNull();
        update!.Version.Should().Be("9.9.9");
        update.DownloadUrl.Should().EndWith("crustcut-v9.9.9-win-x64.zip");
        update.Notes.Should().Be("notes here");
    }

    [Theory]
    [InlineData("v0.9.3")]   // same version
    [InlineData("v0.9.2")]   // older
    public async Task Says_nothing_when_not_newer(string tag)
    {
        (await Checker(Release(tag)).CheckAsync(new Version(0, 9, 3))).Should().BeNull();
    }

    [Fact]
    public async Task Ignores_drafts_and_prereleases()
    {
        (await Checker(Release("v9.9.9", draft: true)).CheckAsync(new Version(0, 9, 3)))
            .Should().BeNull();
        (await Checker(Release("v9.9.9", prerelease: true)).CheckAsync(new Version(0, 9, 3)))
            .Should().BeNull();
    }

    [Fact]
    public async Task Ignores_a_release_with_no_matching_asset()
    {
        // A mac-only release must not be offered to a Windows build.
        var update = await Checker(Release("v9.9.9", assetName: "crustcut-v9.9.9-mac-arm64.zip"))
            .CheckAsync(new Version(0, 9, 3));
        update.Should().BeNull();
    }

    [Fact]
    public async Task Network_and_parse_failures_are_silent()
    {
        (await Checker("", HttpStatusCode.ServiceUnavailable).CheckAsync(new Version(0, 9, 3)))
            .Should().BeNull();
        (await Checker("not json at all").CheckAsync(new Version(0, 9, 3)))
            .Should().BeNull();
    }

    [Theory]
    [InlineData("v1.2.3", true)]
    [InlineData("1.2.3", true)]
    [InlineData("nightly", false)]
    [InlineData("", false)]
    public void Parses_the_tag_formats_releases_actually_use(string tag, bool expected)
    {
        UpdateChecker.TryParseTag(tag, out _).Should().Be(expected);
    }
}
