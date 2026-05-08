using System.Net;
using System.Net.Http;
using System.Text;
using FluentAssertions;
using PrimeOSTuner.Win.SteamGridDb;
using Xunit;

namespace PrimeOSTuner.Tests.SteamGridDb;

public class SteamGridDbClientTests
{
    private static HttpClient FakeHttp(Func<HttpRequestMessage, HttpResponseMessage> handler)
    {
        return new HttpClient(new FakeHandler(handler))
        {
            BaseAddress = new Uri("https://www.steamgriddb.com/")
        };
    }

    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;
        public FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> h) { _handler = h; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
            => Task.FromResult(_handler(req));
    }

    [Fact]
    public async Task GetCoverByAppIdAsync_returns_no_key_status_when_api_key_missing()
    {
        var http = FakeHttp(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var settings = new SteamGridDbSettings { SteamGridDbApiKey = null };
        var client = new SteamGridDbClient(http, settings);

        var art = await client.GetCoverByAppIdAsync("440");

        art.Url.Should().BeNull();
        client.HasApiKey.Should().BeFalse();
    }

    [Fact]
    public async Task GetCoverByAppIdAsync_returns_url_when_api_returns_grid()
    {
        var calls = new List<string>();
        var http = FakeHttp(req =>
        {
            calls.Add(req.RequestUri!.PathAndQuery);
            string body = req.RequestUri!.AbsolutePath.Contains("/games/steam/")
                ? "{\"success\":true,\"data\":{\"id\":99,\"name\":\"Team Fortress 2\"}}"
                : "{\"success\":true,\"data\":[{\"id\":1,\"url\":\"https://cdn.example/cover.jpg\",\"thumb\":\"https://cdn.example/thumb.jpg\",\"width\":600,\"height\":900}]}";
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
        });
        var settings = new SteamGridDbSettings { SteamGridDbApiKey = "test-key" };
        var client = new SteamGridDbClient(http, settings);

        var art = await client.GetCoverByAppIdAsync("440");

        art.Url.Should().Be("https://cdn.example/cover.jpg");
        art.GameId.Should().Be(99);
        calls.Should().HaveCount(2);
        calls[0].Should().Contain("/games/steam/440");
        calls[1].Should().Contain("/grids/game/99");
    }

    [Fact]
    public async Task SearchAsync_uses_autocomplete_endpoint()
    {
        var calls = new List<string>();
        var http = FakeHttp(req =>
        {
            calls.Add(req.RequestUri!.PathAndQuery);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{\"success\":true,\"data\":[{\"id\":42,\"name\":\"Valorant\"}]}",
                    Encoding.UTF8, "application/json")
            };
        });
        var settings = new SteamGridDbSettings { SteamGridDbApiKey = "test-key" };
        var client = new SteamGridDbClient(http, settings);

        var hits = await client.SearchAsync("valorant");

        hits.Should().ContainSingle(h => h.Name == "Valorant" && h.Id == 42);
        calls.Single().Should().Contain("/search/autocomplete/valorant");
    }

    [Fact]
    public async Task GetCoverByAppIdAsync_returns_null_url_when_api_call_fails()
    {
        var http = FakeHttp(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var settings = new SteamGridDbSettings { SteamGridDbApiKey = "test-key" };
        var client = new SteamGridDbClient(http, settings);

        var art = await client.GetCoverByAppIdAsync("440");
        art.Url.Should().BeNull();
    }
}
