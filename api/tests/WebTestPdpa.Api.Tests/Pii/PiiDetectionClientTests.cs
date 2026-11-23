using System.Net;
using Microsoft.Extensions.Options;
using WebTestPdpa.Api.Options;
using WebTestPdpa.Api.Pii;
using WebTestPdpa.Api.Tests.Fakes;

namespace WebTestPdpa.Api.Tests.Pii;

public class PiiDetectionClientTests
{
    private static PiiDetectionClient Create(HttpMessageHandler h, int timeout = 5) =>
        new(new HttpClient(h) { BaseAddress = new Uri("http://pii.test/") },
            Microsoft.Extensions.Options.Options.Create(new PiiOptions { ServiceToken = "test-token", TimeoutSeconds = timeout }));

    [Fact]
    public async Task ParsesOkResponseAndSendsToken()
    {
        var h = FakeHttpMessageHandler.Json(HttpStatusCode.OK,
            """{"entities":[{"type":"PHONE","start":2,"end":6,"score":1.0,"source":"regex"}],"ner_used":true,"elapsed_ms":1}""");
        var r = await Create(h).DetectAsync("ab0812", [], default);
        var e = Assert.Single(r.Entities);
        Assert.Equal(PiiEntityType.Phone, e.Type);
        Assert.True(r.NerUsed);
        Assert.Equal("test-token", h.LastRequest!.Headers.GetValues("X-Internal-Token").Single());
    }

    [Fact]
    public async Task Http500Throws()
    {
        var c = Create(FakeHttpMessageHandler.Json(HttpStatusCode.InternalServerError, "{}"));
        var ex = await Assert.ThrowsAsync<PiiUnavailableException>(() => c.DetectAsync("x", [], default));
        Assert.Equal("HTTP_500", ex.ReasonCode);
    }

    [Fact]
    public async Task BrokenJsonThrows()
    {
        var c = Create(FakeHttpMessageHandler.Json(HttpStatusCode.OK, "not json"));
        await Assert.ThrowsAsync<PiiUnavailableException>(() => c.DetectAsync("x", [], default));
    }

    [Fact]
    public async Task MissingFieldThrows()
    {
        var c = Create(FakeHttpMessageHandler.Json(HttpStatusCode.OK, """{"foo":1}"""));
        await Assert.ThrowsAsync<PiiUnavailableException>(() => c.DetectAsync("x", [], default));
    }

    [Theory]
    [InlineData("""{"entities":[{"type":"PHONE","start":"1","end":3}]}""")]
    [InlineData("""{"entities":[{"type":"PHONE","start":1,"end":"3"}]}""")]
    [InlineData("""{"entities":[1]}""")]
    [InlineData("[]")]
    [InlineData("\"text\"")]
    public async Task WrongJsonShapeThrowsPiiUnavailable_NotInvalidOperation(string body)
    {
        // 🔍 Minor 1: รูปร่าง JSON ผิดต้องเป็น PiiUnavailableException (503) ไม่ใช่ InvalidOperationException (500)
        var c = Create(FakeHttpMessageHandler.Json(HttpStatusCode.OK, body));
        var ex = await Assert.ThrowsAsync<PiiUnavailableException>(() => c.DetectAsync("abcdef", [], default));
        Assert.Equal("BAD_RESPONSE", ex.ReasonCode);
    }

    [Fact]
    public async Task UnknownTypeThrows()
    {
        var c = Create(FakeHttpMessageHandler.Json(HttpStatusCode.OK,
            """{"entities":[{"type":"ALIEN","start":0,"end":1}],"ner_used":false}"""));
        await Assert.ThrowsAsync<PiiUnavailableException>(() => c.DetectAsync("x", [], default));
    }

    [Fact]
    public async Task OffsetBeyondTextThrows()
    {
        var c = Create(FakeHttpMessageHandler.Json(HttpStatusCode.OK,
            """{"entities":[{"type":"PHONE","start":0,"end":50}],"ner_used":false}"""));
        await Assert.ThrowsAsync<PiiUnavailableException>(() => c.DetectAsync("short", [], default));
    }

    [Fact]
    public async Task TimeoutThrowsPiiUnavailable()
    {
        var h = new FakeHttpMessageHandler(async (_, ct) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(30), ct);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var ex = await Assert.ThrowsAsync<PiiUnavailableException>(() => Create(h, timeout: 1).DetectAsync("x", [], default));
        Assert.Equal("TIMEOUT", ex.ReasonCode);
    }

    [Fact]
    public async Task ConnectionErrorThrows()
    {
        var h = new FakeHttpMessageHandler((_, _) => throw new HttpRequestException("boom"));
        await Assert.ThrowsAsync<PiiUnavailableException>(() => Create(h).DetectAsync("x", [], default));
    }
}
