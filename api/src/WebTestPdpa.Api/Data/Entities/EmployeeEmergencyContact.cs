namespace WebTestPdpa.Api.Data.Entities;

/// <summary>📦 ผู้ติดต่อฉุกเฉินของพนักงาน (1:N) — R7 / D33</summary>
public class EmployeeEmergencyContact
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public string Name { get; set; } = "";
    public string Relationship { get; set; } = "";
    public string Phone { get; set; } = "";
}
