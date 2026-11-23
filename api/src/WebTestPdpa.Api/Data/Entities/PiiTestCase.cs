namespace WebTestPdpa.Api.Data.Entities;

public class PiiTestCase
{
    public int Id { get; set; }
    public string CaseKey { get; set; } = "";
    public string Lang { get; set; } = "";
    public string Category { get; set; } = "";
    public string Text { get; set; } = "";
    public string KnownNames { get; set; } = "[]";
    public string Expected { get; set; } = "[]";
    public string? Note { get; set; }
}
