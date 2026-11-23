namespace WebTestPdpa.Api.Data.Entities;

/// <summary>📦 master ตำแหน่งงาน (ข้อมูลสมมติ) — R7 / D33</summary>
public class Position
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string NameTh { get; set; } = "";
    public string NameEn { get; set; } = "";
    public short Level { get; set; }
    public string DepartmentTh { get; set; } = "";
}
