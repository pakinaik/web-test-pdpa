using Microsoft.AspNetCore.Identity;

namespace WebTestPdpa.Api.Data.Entities;

/// <summary>📦 ผู้ใช้งาน (พนักงาน/Admin) — ชื่อ th/en ใช้เป็น known_names ให้ PII service</summary>
public class ApplicationUser : IdentityUser<Guid>
{
    public string? FirstNameTh { get; set; }
    public string? LastNameTh { get; set; }
    public string? FirstNameEn { get; set; }
    public string? LastNameEn { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class ApplicationRole : IdentityRole<Guid>
{
    public ApplicationRole() { }
    public ApplicationRole(string name) : base(name) { }
}
