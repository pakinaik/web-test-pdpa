using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace WebTestPdpa.Api.Data;

/// <summary>🚀 ใช้ตอนสร้าง migration เท่านั้น (dotnet ef) — ไม่มี secret จริง</summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        // 📦 ถ้าไม่มี DB_CONNECTION ใช้ค่า dummy (ไม่ได้เชื่อมต่อจริงตอน generate migration)
        var conn = Environment.GetEnvironmentVariable("DB_CONNECTION") ?? "Host=localhost;Database=design";
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(conn)
            .UseSnakeCaseNamingConvention()
            .Options;
        return new AppDbContext(opts);
    }
}
