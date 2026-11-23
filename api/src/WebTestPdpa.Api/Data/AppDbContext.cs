using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using WebTestPdpa.Api.Data.Entities;

namespace WebTestPdpa.Api.Data;

public class AppDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<PiiVaultEntry> PiiVault => Set<PiiVaultEntry>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<PiiTestCase> PiiTestCases => Set<PiiTestCase>();

    // 📦 R7: ข้อมูลพนักงาน (mock)
    public DbSet<Position> Positions => Set<Position>();
    public DbSet<LeaveType> LeaveTypes => Set<LeaveType>();
    public DbSet<IncomeType> IncomeTypes => Set<IncomeType>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<EmployeeEmergencyContact> EmployeeEmergencyContacts => Set<EmployeeEmergencyContact>();
    public DbSet<EmployeeSalaryHistory> EmployeeSalaryHistory => Set<EmployeeSalaryHistory>();
    public DbSet<EmployeeLeave> EmployeeLeaves => Set<EmployeeLeave>();
    public DbSet<EmployeeMonthlyIncome> EmployeeMonthlyIncomes => Set<EmployeeMonthlyIncome>();

    // 💾 R8: รายงาน PDF (เข้ารหัส + หมดอายุ)
    public DbSet<ReportFile> ReportFiles => Set<ReportFile>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        // 🔄 เปลี่ยนชื่อตาราง Identity เป็น snake_case
        b.Entity<ApplicationUser>(e =>
        {
            e.ToTable("users");
            e.Property(x => x.FirstNameTh).HasMaxLength(100);
            e.Property(x => x.LastNameTh).HasMaxLength(100);
            e.Property(x => x.FirstNameEn).HasMaxLength(100);
            e.Property(x => x.LastNameEn).HasMaxLength(100);
        });
        b.Entity<ApplicationRole>().ToTable("roles");
        b.Entity<Microsoft.AspNetCore.Identity.IdentityUserRole<Guid>>().ToTable("user_roles");
        b.Entity<Microsoft.AspNetCore.Identity.IdentityUserClaim<Guid>>().ToTable("user_claims");
        b.Entity<Microsoft.AspNetCore.Identity.IdentityUserLogin<Guid>>().ToTable("user_logins");
        b.Entity<Microsoft.AspNetCore.Identity.IdentityUserToken<Guid>>().ToTable("user_tokens");
        b.Entity<Microsoft.AspNetCore.Identity.IdentityRoleClaim<Guid>>().ToTable("role_claims");

        b.Entity<Conversation>(e =>
        {
            e.ToTable("conversations");
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).HasMaxLength(60);
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.UserId, x.UpdatedAt }).IsDescending(false, true);
        });

        b.Entity<Message>(e =>
        {
            e.ToTable("messages");
            e.Property(x => x.Role).HasMaxLength(16);
            e.Property(x => x.MaskCounts).HasColumnType("jsonb").HasDefaultValue("{}");
            e.HasOne<Conversation>().WithMany(c => c.Messages).HasForeignKey(x => x.ConversationId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.ConversationId, x.CreatedAt });
            e.HasIndex(x => x.CreatedAt);
        });

        b.Entity<PiiVaultEntry>(e =>
        {
            e.ToTable("pii_vault");
            e.Property(x => x.EntityType).HasMaxLength(20);
            e.Property(x => x.Token).HasMaxLength(40);
            e.Property(x => x.ValueHash).HasColumnType("bytea");
            e.Property(x => x.ValueCipher).HasColumnType("bytea");
            e.Property(x => x.KeyVersion).HasDefaultValue((short)1);
            e.HasOne<Conversation>().WithMany().HasForeignKey(x => x.ConversationId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.ConversationId, x.Token }).IsUnique();
            e.HasIndex(x => new { x.ConversationId, x.EntityType, x.ValueHash }).IsUnique();
            e.HasIndex(x => x.LastUsedAt);
        });

        b.Entity<AuditLog>(e =>
        {
            e.ToTable("audit_logs");
            e.Property(x => x.Id).UseIdentityByDefaultColumn();
            e.Property(x => x.UserName).HasMaxLength(256);
            e.Property(x => x.Action).HasMaxLength(32);
            e.Property(x => x.Status).HasMaxLength(32);
            e.Property(x => x.Provider).HasMaxLength(32);
            e.Property(x => x.Model).HasMaxLength(128);
            e.Property(x => x.MaskCounts).HasColumnType("jsonb").HasDefaultValue("{}");
            e.Property(x => x.OutboundPayload).HasColumnType("jsonb");
            e.Property(x => x.GuardReasons).HasColumnType("jsonb");
            e.Property(x => x.ContextEmployeeIds).HasColumnType("jsonb");
            e.Property(x => x.ContextSections).HasColumnType("jsonb");
            e.Property(x => x.PayloadSha256).HasColumnType("char(64)");
            e.Property(x => x.ErrorCode).HasMaxLength(64);
            // 🔍 FK แบบ set null เมื่อผู้ใช้/บทสนทนา/ข้อความถูกลบ (audit ต้องอยู่ต่อ)
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne<Conversation>().WithMany().HasForeignKey(x => x.ConversationId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne<Message>().WithMany().HasForeignKey(x => x.MessageId).OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(x => x.CreatedAt);
            e.HasIndex(x => x.UserId);
        });

        // 💾 R8: report_files — ลบตาม conversation/message (cascade) และหมดอายุตาม expires_at
        b.Entity<ReportFile>(e =>
        {
            e.ToTable("report_files");
            e.HasKey(x => x.Id);
            e.Property(x => x.Cipher).HasColumnType("bytea");
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Conversation>().WithMany().HasForeignKey(x => x.ConversationId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Message>().WithMany().HasForeignKey(x => x.MessageId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.MessageId);
            e.HasIndex(x => x.ExpiresAt);
        });

        b.Entity<PiiTestCase>(e =>
        {
            e.ToTable("pii_test_cases");
            e.Property(x => x.CaseKey).HasMaxLength(64);
            e.Property(x => x.Lang).HasMaxLength(8);
            e.Property(x => x.Category).HasMaxLength(16);
            e.Property(x => x.KnownNames).HasColumnType("jsonb");
            e.Property(x => x.Expected).HasColumnType("jsonb");
            e.HasIndex(x => x.CaseKey).IsUnique();
        });

        // 📦 R7 (D33): master ตำแหน่ง / ประเภทการลา / ประเภทเงินได้
        b.Entity<Position>(e =>
        {
            e.ToTable("positions");
            e.Property(x => x.Code).HasMaxLength(20);
            e.Property(x => x.NameTh).HasMaxLength(100);
            e.Property(x => x.NameEn).HasMaxLength(100);
            e.Property(x => x.DepartmentTh).HasMaxLength(100);
            e.HasIndex(x => x.Code).IsUnique();
        });

        b.Entity<LeaveType>(e =>
        {
            e.ToTable("leave_types");
            e.Property(x => x.Code).HasMaxLength(20);
            e.Property(x => x.NameTh).HasMaxLength(100);
            e.Property(x => x.NameEn).HasMaxLength(100);
            e.HasIndex(x => x.Code).IsUnique();
        });

        b.Entity<IncomeType>(e =>
        {
            e.ToTable("income_types");
            e.Property(x => x.Code).HasMaxLength(30);
            e.Property(x => x.NameTh).HasMaxLength(100);
            e.Property(x => x.NameEn).HasMaxLength(100);
            e.Property(x => x.Category).HasMaxLength(16);
            e.HasIndex(x => x.Code).IsUnique();
        });

        // 📦 R7 (D33): พนักงาน + ตารางลูก — ⚠️ ไม่แตะตาราง users (ผูกด้วย employees.user_id)
        b.Entity<Employee>(e =>
        {
            e.ToTable("employees");
            e.Property(x => x.EmployeeCode).HasMaxLength(10);
            e.Property(x => x.TitleTh).HasMaxLength(100);
            e.Property(x => x.FirstNameTh).HasMaxLength(100);
            e.Property(x => x.LastNameTh).HasMaxLength(100);
            e.Property(x => x.TitleEn).HasMaxLength(100);
            e.Property(x => x.FirstNameEn).HasMaxLength(100);
            e.Property(x => x.LastNameEn).HasMaxLength(100);
            e.Property(x => x.NationalId).HasColumnType("char(13)");
            e.Property(x => x.TaxId).HasColumnType("char(13)");
            e.Property(x => x.SocialSecurityNo).HasColumnType("char(13)");
            e.Property(x => x.Phone).HasMaxLength(20);
            e.Property(x => x.Email).HasMaxLength(256);
            e.Property(x => x.Gender).HasMaxLength(16);
            e.Property(x => x.Address).HasMaxLength(500);
            e.Property(x => x.Nationality).HasMaxLength(50);
            e.Property(x => x.MaritalStatus).HasMaxLength(30);
            e.Property(x => x.Religion).HasMaxLength(50);
            e.Property(x => x.BloodType).HasMaxLength(3);
            e.Property(x => x.ChronicDisease).HasMaxLength(200);
            e.Property(x => x.Ethnicity).HasMaxLength(50);
            e.Property(x => x.BankName).HasMaxLength(100);
            e.Property(x => x.BankAccountNo).HasMaxLength(20);
            e.HasIndex(x => x.EmployeeCode).IsUnique();
            e.HasIndex(x => x.NationalId).IsUnique();
            // 🔄 unique เฉพาะที่มีค่า (postgres ยอมให้ NULL ซ้ำได้)
            e.HasIndex(x => x.UserId).IsUnique();
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.Position).WithMany().HasForeignKey(x => x.PositionId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.EmergencyContacts).WithOne().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.SalaryHistory).WithOne().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Leaves).WithOne().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.MonthlyIncomes).WithOne().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<EmployeeEmergencyContact>(e =>
        {
            e.ToTable("employee_emergency_contacts");
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.Relationship).HasMaxLength(50);
            e.Property(x => x.Phone).HasMaxLength(20);
        });

        b.Entity<EmployeeSalaryHistory>(e =>
        {
            e.ToTable("employee_salary_history");
            e.Property(x => x.BaseSalary).HasPrecision(12, 2);
            e.HasOne<Position>().WithMany().HasForeignKey(x => x.PositionId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.EmployeeId, x.EffectiveDate });
        });

        b.Entity<EmployeeLeave>(e =>
        {
            e.ToTable("employee_leaves");
            e.Property(x => x.Days).HasPrecision(4, 1);
            e.Property(x => x.Status).HasMaxLength(16);
            e.HasOne<LeaveType>().WithMany().HasForeignKey(x => x.LeaveTypeId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.EmployeeId, x.StartDate });
        });

        b.Entity<EmployeeMonthlyIncome>(e =>
        {
            e.ToTable("employee_monthly_incomes");
            e.Property(x => x.Id).UseIdentityByDefaultColumn();
            e.Property(x => x.Amount).HasPrecision(12, 2);
            e.HasOne<IncomeType>().WithMany().HasForeignKey(x => x.IncomeTypeId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.EmployeeId, x.IncomeTypeId, x.PayMonth }).IsUnique();
        });
    }
}
