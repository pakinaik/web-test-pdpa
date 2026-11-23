namespace WebTestPdpa.Api.Data.Entities;

/// <summary>
/// 📦 ใบลาของพนักงาน — ⚠️ ไม่มี column เหตุผลการลา (กันข้อมูลสุขภาพในข้อความอิสระ) R7 / D33
/// </summary>
public class EmployeeLeave
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public int LeaveTypeId { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public decimal Days { get; set; }
    /// <summary>🔍 approved / pending / rejected</summary>
    public string Status { get; set; } = "approved";
}
