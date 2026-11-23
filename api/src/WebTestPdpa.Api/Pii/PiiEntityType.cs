namespace WebTestPdpa.Api.Pii;

/// <summary>
/// 🔍 ประเภทข้อมูลส่วนบุคคลที่ระบบ mask
/// ⚠️ Person..BankAccount = ประเภทที่ pii service ตรวจได้ (detector type), Custom = ผู้ใช้กำหนดเอง,
/// ส่วนที่ตามหลัง Custom (R7 / D41) = ใช้ฝั่ง .NET เท่านั้น (ข้อมูลพนักงาน/ข้อมูลอ่อนไหวมาตรา 26) — pii ห้ามส่งมา
/// </summary>
public enum PiiEntityType
{
    Person,
    Phone,
    ThaiId,
    Gender,
    Email,
    BankAccount,
    Custom,
    Religion,
    Ethnicity,
    Health,
    BloodType,
    Nationality,
    MaritalStatus,
    Address,
    DateOfBirth,
    EmployeeId
}

/// <summary>🔄 แปลงระหว่าง enum กับชื่อที่ใช้ใน wire / token</summary>
public static class PiiEntityTypes
{
    public static string ToWire(PiiEntityType type) => type switch
    {
        PiiEntityType.Person => "PERSON",
        PiiEntityType.Phone => "PHONE",
        PiiEntityType.ThaiId => "THAI_ID",
        PiiEntityType.Gender => "GENDER",
        PiiEntityType.Email => "EMAIL",
        PiiEntityType.BankAccount => "BANK_ACCOUNT",
        PiiEntityType.Custom => "CUSTOM",
        PiiEntityType.Religion => "RELIGION",
        PiiEntityType.Ethnicity => "ETHNICITY",
        PiiEntityType.Health => "HEALTH",
        PiiEntityType.BloodType => "BLOOD_TYPE",
        PiiEntityType.Nationality => "NATIONALITY",
        PiiEntityType.MaritalStatus => "MARITAL_STATUS",
        PiiEntityType.Address => "ADDRESS",
        PiiEntityType.DateOfBirth => "DATE_OF_BIRTH",
        PiiEntityType.EmployeeId => "EMPLOYEE_ID",
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };

    public static bool TryParse(string? wire, out PiiEntityType type)
    {
        switch (wire?.ToUpperInvariant())
        {
            case "PERSON": type = PiiEntityType.Person; return true;
            case "PHONE": type = PiiEntityType.Phone; return true;
            case "THAI_ID": type = PiiEntityType.ThaiId; return true;
            case "GENDER": type = PiiEntityType.Gender; return true;
            case "EMAIL": type = PiiEntityType.Email; return true;
            case "BANK_ACCOUNT": type = PiiEntityType.BankAccount; return true;
            case "CUSTOM": type = PiiEntityType.Custom; return true;
            case "RELIGION": type = PiiEntityType.Religion; return true;
            case "ETHNICITY": type = PiiEntityType.Ethnicity; return true;
            case "HEALTH": type = PiiEntityType.Health; return true;
            case "BLOOD_TYPE": type = PiiEntityType.BloodType; return true;
            case "NATIONALITY": type = PiiEntityType.Nationality; return true;
            case "MARITAL_STATUS": type = PiiEntityType.MaritalStatus; return true;
            case "ADDRESS": type = PiiEntityType.Address; return true;
            case "DATE_OF_BIRTH": type = PiiEntityType.DateOfBirth; return true;
            case "EMPLOYEE_ID": type = PiiEntityType.EmployeeId; return true;
            default: type = default; return false;
        }
    }

    /// <summary>🔍 true = ประเภทที่ pii service ส่งกลับมาได้ (PERSON..BANK_ACCOUNT) — ประเภทอื่น (รวม CUSTOM) pii ห้ามส่ง = fail-closed</summary>
    public static bool IsDetectorType(PiiEntityType type) => type <= PiiEntityType.BankAccount;
}
