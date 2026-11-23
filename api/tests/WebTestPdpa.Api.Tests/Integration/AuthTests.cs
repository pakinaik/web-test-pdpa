using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using WebTestPdpa.Api.Data.Entities;

namespace WebTestPdpa.Api.Tests.Integration;

[Collection("pg")]
[Trait("Category", "Integration")]
public class AuthTests : IAsyncLifetime
{
    private readonly PostgresFixture _pg;
    private ApiFactory _f = null!;

    public AuthTests(PostgresFixture pg) => _pg = pg;

    public async Task InitializeAsync() => _f = new ApiFactory(await _pg.CreateDatabaseAsync());
    public async Task DisposeAsync() => await _f.DisposeAsync();

    [Fact]
    public async Task LoginOk_SetsHttpOnlyStrictCookie()
    {
        var c = _f.CreateClient();
        var r = await c.PostAsJsonAsync("/api/auth/login", new { userName = _f.AdminUser, password = _f.AdminPassword });
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var cookie = Assert.Single(r.Headers.GetValues("Set-Cookie"), v => v.StartsWith("access_token="));
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
        var body = await r.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal("Admin", body.GetProperty("roles")[0].GetString());
    }

    [Fact]
    public async Task LoginWrong_401()
    {
        var c = _f.CreateClient();
        var r = await c.PostAsJsonAsync("/api/auth/login", new { userName = _f.AdminUser, password = "wrong-password" });
        Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
        Assert.Contains("INVALID_CREDENTIALS", await r.Content.ReadAsStringAsync());
        var r2 = await c.PostAsJsonAsync("/api/auth/login", new { userName = "nobody", password = "wrong-password" });
        Assert.Equal(HttpStatusCode.Unauthorized, r2.StatusCode);
    }

    [Fact]
    public async Task Me_WithoutCookie401_WithCookie200()
    {
        var c = _f.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/auth/me")).StatusCode);
        await c.PostAsJsonAsync("/api/auth/login", new { userName = _f.AdminUser, password = _f.AdminPassword });
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Logout_ClearsCookie()
    {
        var c = _f.CreateClient();
        await c.PostAsJsonAsync("/api/auth/login", new { userName = _f.AdminUser, password = _f.AdminPassword });
        Assert.Equal(HttpStatusCode.NoContent, (await c.PostAsync("/api/auth/logout", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Employee_Gets403OnAdmin_AdminGets200()
    {
        var pwd = "Bb2@" + Guid.NewGuid().ToString("N")[..8];
        using (var scope = _f.Services.CreateScope())
        {
            var um = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var u = new ApplicationUser { UserName = "emp_test" };
            Assert.True((await um.CreateAsync(u, pwd)).Succeeded);
            await um.AddToRoleAsync(u, "Employee");
        }
        var emp = _f.CreateClient();
        await emp.PostAsJsonAsync("/api/auth/login", new { userName = "emp_test", password = pwd });
        Assert.Equal(HttpStatusCode.Forbidden, (await emp.GetAsync("/api/admin/users")).StatusCode);

        var adm = _f.CreateClient();
        await adm.PostAsJsonAsync("/api/auth/login", new { userName = _f.AdminUser, password = _f.AdminPassword });
        Assert.Equal(HttpStatusCode.OK, (await adm.GetAsync("/api/admin/users")).StatusCode);
    }

    [Fact]
    public async Task AdminEndpointWithoutLogin_401()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await _f.CreateClient().GetAsync("/api/admin/users")).StatusCode);
    }

    [Fact]
    public async Task Seed_CreatesTestCases()
    {
        using var scope = _f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WebTestPdpa.Api.Data.AppDbContext>();
        Assert.True(db.PiiTestCases.Count() >= 50);
    }
}
