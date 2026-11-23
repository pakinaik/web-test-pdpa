namespace WebTestPdpa.Api.Data.Entities;

/// <summary>📦 ประวัติเงินเดือน (มีผลตั้งแต่ effective_date) — R7 / D33</summary>
public class EmployeeSalaryHistory
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public DateOnly EffectiveDate { get; set; }
    public decimal BaseSalary { get; set; }
    public int PositionId { get; set; }
}
