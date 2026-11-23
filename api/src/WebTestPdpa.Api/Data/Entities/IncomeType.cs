namespace WebTestPdpa.Api.Data.Entities;

/// <summary>📦 master ประเภทเงินได้ (category = regular / irregular / special) — R7 / D33</summary>
public class IncomeType
{
    public const string Regular = "regular";
    public const string Irregular = "irregular";
    public const string Special = "special";

    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string NameTh { get; set; } = "";
    public string NameEn { get; set; } = "";
    public string Category { get; set; } = Regular;
}
