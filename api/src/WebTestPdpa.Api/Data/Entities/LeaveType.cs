namespace WebTestPdpa.Api.Data.Entities;

/// <summary>📦 master ประเภทการลา + จำนวนวันต่อปี — R7 / D33</summary>
public class LeaveType
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string NameTh { get; set; } = "";
    public string NameEn { get; set; } = "";
    public short DaysPerYear { get; set; }
    public bool IsPaid { get; set; }
}
