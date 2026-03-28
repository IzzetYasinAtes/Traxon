using System.Net;
using System.Text;

namespace Traxon.CryptoTrader.Polymarket.Tests.Helpers;

public sealed class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

    public MockHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        => _handler = handler;

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken ct)
        => Task.FromResult(_handler(request));

    public static HttpClient CreateClient(
        string jsonResponse,
        HttpStatusCode statusCode = HttpStatusCode.OK,
        string baseAddress = "https://clob.polymarket.com")
    {
        var handler = new MockHttpMessageHandler(_ =>
            new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(jsonResponse, Encoding.UTF8, "application/json")
            });
        return new HttpClient(handler) { BaseAddress = new Uri(baseAddress) };
    }
}
