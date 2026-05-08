using System.Net;
using System.Net.Http;
using FluentAssertions;
using PrimeOSTuner.Win.SteamGridDb;
using Xunit;

namespace PrimeOSTuner.Tests.SteamGridDb;

public class ArtCacheTests : IDisposable
{
    private readonly string _cacheDir = Path.Combine(Path.GetTempPath(), $"art-{Guid.NewGuid()}");

    public void Dispose() { if (Directory.Exists(_cacheDir)) Directory.Delete(_cacheDir, true); }

    private sealed class StaticHandler : HttpMessageHandler
    {
        private readonly byte[] _bytes;
        public int CallCount { get; private set; }
        public StaticHandler(byte[] bytes) { _bytes = bytes; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
        {
            CallCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(_bytes)
            });
        }
    }

    [Fact]
    public async Task GetOrDownloadAsync_returns_cached_path_after_first_call()
    {
        var bytes = new byte[] { 1, 2, 3, 4 };
        var handler = new StaticHandler(bytes);
        var http = new HttpClient(handler);
        var cache = new ArtCache(_cacheDir, http);

        var path1 = await cache.GetOrDownloadAsync(123, "https://example/x.jpg");
        var path2 = await cache.GetOrDownloadAsync(123, "https://example/x.jpg");

        path1.Should().Be(path2);
        File.Exists(path1!).Should().BeTrue();
        handler.CallCount.Should().Be(1);
        File.ReadAllBytes(path1!).Should().BeEquivalentTo(bytes);
    }

    [Fact]
    public async Task GetOrDownloadAsync_returns_null_when_url_is_null()
    {
        var http = new HttpClient(new StaticHandler(Array.Empty<byte>()));
        var cache = new ArtCache(_cacheDir, http);

        var path = await cache.GetOrDownloadAsync(456, null);
        path.Should().BeNull();
    }
}
