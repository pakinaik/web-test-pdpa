namespace WebTestPdpa.Api.Data.Entities;

/// <summary>📦 เงินได้รายเดือนต่อประเภท (pay_month = วันที่ 1 ของเดือน) — R7 / D33</summary>
public class EmployeeMonthlyIncome
{
    public long Id { get; set; }
    public int EmployeeId { get; set; }
    public int IncomeTypeId { get; set; }
    public DateOnly PayMonth { get; set; }
    public decimal Amount { get; set; }
}
