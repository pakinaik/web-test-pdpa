using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Threading.RateLimiting;
using WebTestPdpa.Api.Admin;
using WebTestPdpa.Api.Auth;
using WebTestPdpa.Api.Background;
using WebTestPdpa.Api.Chat;
using WebTestPdpa.Api.Data;
using WebTestPdpa.Api.Data.Entities;
using WebTestPdpa.Api.Employees;
using WebTestPdpa.Api.Endpoints;
using WebTestPdpa.Api.Llm;
using WebTestPdpa.Api.Options;
using WebTestPdpa.Api.Pii;
using WebTestPdpa.Api.Reports;

var builder = WebApplication.CreateBuilder(args);

// 🚀 options จาก env + validate ตอน start
builder.Services.AddAppOptions();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddProblemDetails();

// 🚀 DB (⚠️ ห้าม EnableSensitiveDataLogging)
builder.Services.AddDbContext<AppDbContext>((sp, opt) =>
    opt.UseNpgsql(sp.GetRequiredService<IOptions<DbOptions>>().Value.Connection)
       .UseSnakeCaseNamingConvention());

// 🚀 Identity (AddIdentityCore เพื่อไม่ชนกับ JwtBearer)
builder.Services.AddIdentityCore<ApplicationUser>(o =>
    {
        o.Password.RequiredLength = 10;
        o.Lockout.MaxFailedAccessAttempts = 5;
        o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        o.Lockout.AllowedForNewUsers = true;
        o.User.RequireUniqueEmail = false;
    })
    .AddRoles<ApplicationRole>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddSignInManager();

// ⚠️ JWT จาก cookie access_token
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtOptions>>((o, jwtOpt) =>
    {
        var jwt = jwtOpt.Value;
        o.MapInboundClaims = false;
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            RoleClaimType = "role",
            NameClaimType = "name"
        };
        o.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                // 🔄 อ่าน token จาก httpOnly cookie
                ctx.Token = ctx.Request.Cookies[AuthCookie.Name];
                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("Admin", p => p.RequireRole("Admin"));

builder.Services.AddSingleton<JwtTokenService>();
builder.Services.AddSingleton<AuthCookie>();

// 🚀 PII core
builder.Services.AddSingleton(sp => VaultCrypto.FromBase64(sp.GetRequiredService<IOptions<VaultOptions>>().Value.EncryptionKey));
builder.Services.AddHttpClient<IPiiDetectionClient, PiiDetectionClient>((sp, c) =>
{
    var o = sp.GetRequiredService<IOptions<PiiOptions>>().Value;
    c.BaseAddress = new Uri(o.BaseUrl.TrimEnd('/') + "/");
});
builder.Services.AddScoped<IVaultStore, EfVaultStore>();
builder.Services.AddScoped<MaskingService>();
builder.Services.AddScoped<UnmaskService>();
builder.Services.AddSingleton<OutboundGuard>();

// 🚀 LLM gateway (⚠️ ห้ามใช้ UseLogging ของ M.E.AI — จะ log เนื้อหา)
builder.Services.AddSingleton<IChatClient>(sp => LlmClientFactory.Create(sp.GetRequiredService<IOptions<LlmOptions>>().Value));
builder.Services.AddSingleton<ILlmGateway, LlmGateway>();

builder.Services.AddMemoryCache();
builder.Services.AddScoped<KnownNamesProvider>();
builder.Services.AddScoped<EmployeeTargetResolver>();
builder.Services.AddScoped<EmployeeContextBuilder>();
builder.Services.AddScoped<ChatService>();
// 🚀 R8: รายงาน PDF (key derive จาก VAULT_ENCRYPTION_KEY ไม่ต้องมี env ใหม่)
builder.Services.AddSingleton(sp => ReportCrypto.FromBase64(sp.GetRequiredService<IOptions<VaultOptions>>().Value.EncryptionKey));
builder.Services.AddSingleton<IReportRenderer, QuestPdfReportRenderer>();
builder.Services.AddScoped<ReportService>();
builder.Services.AddScoped<PiiBenchService>();
builder.Services.AddSingleton<RetentionService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<RetentionService>());

// 🚀 AL11: rate limit ต่อ user (policy chat)
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("chat", ctx =>
    {
        var limit = ctx.RequestServices.GetRequiredService<IOptions<ChatLimitOptions>>().Value.RateLimitPerMinute;
        var key = ctx.User.FindFirst("sub")?.Value ?? "anonymous";
        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = limit,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        });
    });
    o.OnRejected = async (ctx, ct) =>
    {
        ctx.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await WebTestPdpa.Api.Contracts.ApiErrors.Problem(StatusCodes.Status429TooManyRequests, "RATE_LIMITED")
            .ExecuteAsync(ctx.HttpContext);
    };
});

var app = builder.Build();

// ⚠️ ProblemDetails กลาง ไม่แสดง stack/ข้อความ exception
app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapAuthEndpoints();
app.MapAdminEndpoints();
app.MapConversationEndpoints();
app.MapChatEndpoints();
app.MapReportEndpoints();
app.MapHealthEndpoints();

// 🚀 migrate + seed
await DbInitializer.InitializeAsync(app.Services);

app.Run();

// 🚀 ให้ WebApplicationFactory เข้าถึงได้
public partial class Program;
