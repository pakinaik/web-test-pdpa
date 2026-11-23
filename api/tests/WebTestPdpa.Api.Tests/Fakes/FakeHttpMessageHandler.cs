using System.Net;

namespace WebTestPdpa.Api.Tests.Fakes;

/// <summary>📤 handler ปลอมสำหรับทดสอบ HttpClient โดยไม่ออก network</summary>
public sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;
    public HttpRequestMessage? LastRequest { get; private set; }

    public FakeHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler) => _handler = handler;

    public static FakeHttpMessageHandler Json(HttpStatusCode code, string body) =>
        new((_, _) => Task.FromResult(new HttpResponseMessage(code) { Content = new StringContent(body) }));

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequest = request;
        return _handler(request, cancellationToken);
    }
}
