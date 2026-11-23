using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using WebTestPdpa.Api.Auth;
using WebTestPdpa.Api.Contracts;
using WebTestPdpa.Api.Data.Entities;

namespace WebTestPdpa.Api.Endpoints;

public static class AuthEndpoints
{
    public static string DisplayName(ApplicationUser u)
    {
        var th = $"{u.FirstNameTh} {u.LastNameTh}".Trim();
        return th.Length > 0 ? th : u.UserName ?? "";
    }

    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/auth");

        g.MapPost("/login", async (LoginRequest req, UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn,
            JwtTokenService jwt, AuthCookie cookie, HttpResponse resp) =>
        {
            // 🔍 ข้อความเดียวกันทุกกรณี (ไม่มี user / password ผิด / locked / inactive)
            var invalid = ApiErrors.Problem(StatusCodes.Status401Unauthorized, "INVALID_CREDENTIALS");
            if (string.IsNullOrWhiteSpace(req.UserName) || string.IsNullOrEmpty(req.Password)) return invalid;

            var user = await users.FindByNameAsync(req.UserName);
            if (user is null || !user.IsActive) return invalid;

            var result = await signIn.CheckPasswordSignInAsync(user, req.Password, lockoutOnFailure: true);
            if (!result.Succeeded) return invalid;

            var roles = await users.GetRolesAsync(user);
            var (token, expires) = jwt.Create(user, roles);
            cookie.Write(resp, token, expires);
            return Results.Ok(new UserInfo(user.Id, user.UserName ?? "", DisplayName(user), roles.ToList()));
        }).AllowAnonymous();

        g.MapPost("/logout", (AuthCookie cookie, HttpResponse resp) =>
        {
            cookie.Clear(resp);
            return Results.NoContent();
        }).AllowAnonymous();

        g.MapGet("/me", async (ClaimsPrincipal principal, UserManager<ApplicationUser> users) =>
        {
            var sub = principal.FindFirstValue("sub");
            if (!Guid.TryParse(sub, out var id)) return Results.Unauthorized();
            var user = await users.FindByIdAsync(id.ToString());
            if (user is null || !user.IsActive) return Results.Unauthorized();
            var roles = await users.GetRolesAsync(user);
            return Results.Ok(new UserInfo(user.Id, user.UserName ?? "", DisplayName(user), roles.ToList()));
        }).RequireAuthorization();
    }
}
