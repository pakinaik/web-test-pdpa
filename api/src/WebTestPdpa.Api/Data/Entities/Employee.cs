namespace WebTestPdpa.Api.Data.Entities;

/// <summary>
/// 📦 ข้อมูลพนักงาน (mock — ข้อมูลสมมติทั้งหมด) R7 / D33
/// ⚠️ เก็บ plaintext ตาม D34 (จำลองระบบ HR ต้นทาง) — ห้ามมี column ข้อความอิสระ และห้าม log ค่าใดๆ
/// </summary>
public class Employee
{
    public int Id { get; set; }
    /// <summary>🔍 รหัสพนักงาน เช่น emp001 (= username ของ user ที่ผูก)</summary>
    public string EmployeeCode { get; set; } = "";
    /// <summary>🔄 ผูก user ผ่าน column นี้ (ไม่แก้ตาราง users) — SetNull เมื่อ user ถูกลบ</summary>
    public Guid? UserId { get; set; }

    public string TitleTh { get; set; } = "";
    public string FirstNameTh { get; set; } = "";
    public string LastNameTh { get; set; } = "";
    public string TitleEn { get; set; } = "";
    public string FirstNameEn { get; set; } = "";
    public string LastNameEn { get; set; } = "";

    public string NationalId { get; set; } = "";
    public string TaxId { get; set; } = "";
    public string SocialSecurityNo { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Email { get; set; } = "";
    public string Gender { get; set; } = "";
    public DateOnly DateOfBirth { get; set; }
    public string Address { get; set; } = "";
    public string Nationality { get; set; } = "";
    public string MaritalStatus { get; set; } = "";

    // ⚠️ ข้อมูลอ่อนไหว (PDPA มาตรา 26)
    public string Religion { get; set; } = "";
    public string BloodType { get; set; } = "";
    public string ChronicDisease { get; set; } = "";
    public string Ethnicity { get; set; } = "";

    public string BankName { get; set; } = "";
    public string BankAccountNo { get; set; } = "";

    public int PositionId { get; set; }
    public Position? Position { get; set; }
    public DateOnly HireDate { get; set; }
    public bool IsActive { get; set; } = true;

    public List<EmployeeEmergencyContact> EmergencyContacts { get; set; } = [];
    public List<EmployeeSalaryHistory> SalaryHistory { get; set; } = [];
    public List<EmployeeLeave> Leaves { get; set; } = [];
    public List<EmployeeMonthlyIncome> MonthlyIncomes { get; set; } = [];
}
