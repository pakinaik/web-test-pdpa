using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using WebTestPdpa.Api.Data.Entities;

namespace WebTestPdpa.Api.Tests.Integration;

[Collection("pg")]
[Trait("Category", "Integration")]
public class AdminTests : IAsyncLifetime
{
    private readonly PostgresFixture _pg;
    private ApiFactory _f = null!;
    private HttpClient _admin = null!;

    public AdminTests(PostgresFixture pg) => _pg = pg;

    public async Task InitializeAsync()
    {
        _f = new ApiFactory(await _pg.CreateDatabaseAsync());
        _admin = await _f.LoginAdminAsync();
    }

    public async Task DisposeAsync() => await _f.DisposeAsync();

    [Fact]
    public async Task CreateUser_CanLogin_AndNameBecomesKnownName()
    {
        var pwd = "Dd4$" + Guid.NewGuid().ToString("N")[..10];
        var r = await _admin.PostAsJsonAsync("/api/admin/users", new
        {
            userName = "emp01", password = pwd, firstNameTh = "วิชัย", lastNameTh = "มั่นคง",
            firstNameEn = "Wichai", lastNameEn = "Mankhong", role = "Employee"
        });
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);

        var emp = _f.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await emp.PostAsJsonAsync("/api/auth/login", new { userName = "emp01", password = pwd })).StatusCode);
        await emp.PostAsJsonAsync("/api/chat/send", new { text = "สวัสดี" });
        var names = _f.Pii.SeenKnownNames.Last();
        Assert.Contains("วิชัย มั่นคง", names);
        Assert.Contains("Wichai Mankhong", names);
    }

    [Fact]
    public async Task CreateUser_DuplicateAndWeakPassword()
    {
        var pwd = "Ee5%" + Guid.NewGuid().ToString("N")[..10];
        await _admin.PostAsJsonAsync("/api/admin/users", new { userName = "dup", password = pwd, role = "Employee" });
        var d = await _admin.PostAsJsonAsync("/api/admin/users", new { userName = "dup", password = pwd, role = "Employee" });
        Assert.Equal(HttpStatusCode.Conflict, d.StatusCode);
        Assert.Contains("DUPLICATE_USER", await d.Content.ReadAsStringAsync());
        var w = await _admin.PostAsJsonAsync("/api/admin/users", new { userName = "weak", password = "abc", role = "Employee" });
        Assert.Equal(HttpStatusCode.BadRequest, w.StatusCode);
    }

    [Fact]
    public async Task AuditLogs_PagingAndDetail()
    {
        var emp = await _f.LoginEmployeeAsync("emp_audit");
        for (var i = 0; i < 3; i++)
            await emp.PostAsJsonAsync("/api/chat/send", new { text = $"ข้อความ {i} เบอร์ 081-234-567{i}" });

        var list = await (await _admin.GetAsync("/api/admin/audit-logs?page=1&pageSize=2")).JsonAsync();
        Assert.Equal(3, list.GetProperty("total").GetInt32());
        Assert.Equal(2, list.GetProperty("items").GetArrayLength());
        var first = list.GetProperty("items")[0];
        Assert.False(first.TryGetProperty("outboundPayload", out _));
        Assert.Equal("emp_audit", first.GetProperty("userName").GetString());
        Assert.Equal("chat_send", first.GetProperty("action").GetString());
        Assert.Equal(1, (await (await _admin.GetAsync("/api/admin/audit-logs?page=2&pageSize=2")).JsonAsync()).GetProperty("items").GetArrayLength());

        var id = first.GetProperty("id").GetInt64();
        var detail = await (await _admin.GetAsync($"/api/admin/audit-logs/{id}")).JsonAsync();
        Assert.True(detail.GetProperty("outboundPayload").GetArrayLength() >= 2);
        Assert.Equal(64, detail.GetProperty("payloadSha256").GetString()!.Length);
        Assert.Equal(HttpStatusCode.NotFound, (await _admin.GetAsync("/api/admin/audit-logs/999999")).StatusCode);
    }

    [Fact]
    public async Task PiiBench_ComputesPrecisionRecallAsExpected()
    {
        await _f.WithDb(async db =>
        {
            db.PiiTestCases.RemoveRange(db.PiiTestCases);
            db.PiiTestCases.AddRange(
                new PiiTestCase { CaseKey = "a", Lang = "th", Category = "basic", Text = "คุณสมชาย โทร 081-234-5678", KnownNames = """["สมชาย"]""",
                    Expected = """[{"type":"PERSON","value":"สมชาย"},{"type":"PHONE","value":"081-234-5678"}]""" },
                new PiiTestCase { CaseKey = "b", Lang = "th", Category = "negative", Text = "ไม่มีข้อมูล 0899999999", Expected = "[]" },
                new PiiTestCase { CaseKey = "c", Lang = "en", Category = "basic", Text = "mail x@example.com", Expected = """[{"type":"EMAIL","value":"x@example.com"}]""" },
                new PiiTestCase { CaseKey = "d", Lang = "th", Category = "basic", Text = "เพศชาย", Expected = """[{"type":"GENDER","value":"ชาย"}]""" });
            await db.SaveChangesAsync();
            return 0;
        });
        var j = await (await _admin.PostAsync("/api/admin/pii-tests/run", null)).JsonAsync();
        Assert.Equal(4, j.GetProperty("totalCases").GetInt32());
        var per = j.GetProperty("perType").EnumerateArray().ToDictionary(x => x.GetProperty("type").GetString()!);
        Assert.Equal(1, per["PHONE"].GetProperty("tp").GetInt32());
        Assert.Equal(1, per["PHONE"].GetProperty("fp").GetInt32());
        Assert.Equal(0.5, per["PHONE"].GetProperty("precision").GetDouble());
        Assert.Equal(1, per["GENDER"].GetProperty("fn").GetInt32());
        Assert.Equal(0.0, per["GENDER"].GetProperty("recall").GetDouble());
        Assert.Equal(1, per["PERSON"].GetProperty("tp").GetInt32());
        Assert.Equal(3, j.GetProperty("overall").GetProperty("tp").GetInt32());
        Assert.Equal(2, j.GetProperty("failures").GetArrayLength());

        _f.Pii.Throw = true;
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await _admin.PostAsync("/api/admin/pii-tests/run", null)).StatusCode);
    }

    [Fact]
    public async Task PiiTests_ListSeeded()
    {
        var list = await (await _admin.GetAsync("/api/admin/pii-tests")).JsonAsync();
        Assert.True(list.GetArrayLength() >= 50);
    }
}
