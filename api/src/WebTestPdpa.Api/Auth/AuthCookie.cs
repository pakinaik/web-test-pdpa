using Microsoft.Extensions.Options;
using WebTestPdpa.Api.Options;

namespace WebTestPdpa.Api.Auth;

/// <summary>⚠️ cookie access_token: HttpOnly + SameSite=Strict (F7)</summary>
public sealed class AuthCookie
{
    public const string Name = "access_token";
    private readonly JwtOptions _options;

    public AuthCookie(IOptions<JwtOptions> options) => _options = options.Value;

    public void Write(HttpResponse response, string token, DateTimeOffset expires) =>
        response.Cookies.Append(Name, token, new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Strict,
            Secure = _options.CookieSecure,
            Path = "/",
            Expires = expires
        });

    public void Clear(HttpResponse response) =>
        response.Cookies.Delete(Name, new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Strict,
            Secure = _options.CookieSecure,
            Path = "/"
        });
}
