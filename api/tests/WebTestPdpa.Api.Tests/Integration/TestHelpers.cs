using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using WebTestPdpa.Api.Data;
using WebTestPdpa.Api.Data.Entities;

namespace WebTestPdpa.Api.Tests.Integration;

public static class TestHelpers
{
    public static async Task<HttpClient> LoginAdminAsync(this ApiFactory f)
    {
        var c = f.CreateClient();
        var r = await c.PostAsJsonAsync("/api/auth/login", new { userName = f.AdminUser, password = f.AdminPassword });
        r.EnsureSuccessStatusCode();
        return c;
    }

    /// <summary>🚀 สร้าง Employee (รหัสผ่านสุ่มใน test) แล้ว login</summary>
    public static async Task<HttpClient> LoginEmployeeAsync(this ApiFactory f, string userName)
    {
        var pwd = "Cc3#" + Guid.NewGuid().ToString("N")[..10];
        using (var scope = f.Services.CreateScope())
        {
            var um = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var u = new ApplicationUser { UserName = userName };
            var res = await um.CreateAsync(u, pwd);
            if (!res.Succeeded) throw new InvalidOperationException("create user failed");
            await um.AddToRoleAsync(u, "Employee");
        }
        var c = f.CreateClient();
        (await c.PostAsJsonAsync("/api/auth/login", new { userName, password = pwd })).EnsureSuccessStatusCode();
        return c;
    }

    public static async Task<T> WithDb<T>(this ApiFactory f, Func<AppDbContext, Task<T>> fn)
    {
        using var scope = f.Services.CreateScope();
        return await fn(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    public static async Task<JsonElement> JsonAsync(this HttpResponseMessage r) =>
        await r.Content.ReadFromJsonAsync<JsonElement>();
}
