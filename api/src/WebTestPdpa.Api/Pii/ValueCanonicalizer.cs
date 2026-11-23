namespace WebTestPdpa.Api.Pii;

/// <summary>🔄 AL4: ทำค่าให้เป็นรูป canonical — ใช้คำนวณ HMAC เท่านั้น (ไม่ใช่ค่าที่เก็บ)</summary>
public static class ValueCanonicalizer
{
    /// <summary>🔄 N1: canonical กลาง (normalize + collapse whitespace + trim + lower) ใช้กับ span ที่ถูก union ขยายแล้ว</summary>
    public static string CanonicalGeneric(string value)
    {
        var v = TextNormalizer.Normalize(value);
        return PiiRegex.Whitespace.Replace(v, " ").Trim().ToLowerInvariant();
    }

    public static string Canonical(PiiEntityType type, string value)
    {
        var v = TextNormalizer.Normalize(value);
        switch (type)
        {
            case PiiEntityType.Phone:
            {
                var d = PiiRegex.NonDigit.Replace(v, "");
                // 🔄 +66 81... -> 081...
                if (d.StartsWith("66", StringComparison.Ordinal) && d.Length == 11)
                {
                    d = "0" + d[2..];
                }
                return d;
            }
            case PiiEntityType.ThaiId:
            case PiiEntityType.BankAccount:
                return PiiRegex.NonDigit.Replace(v, "");
            case PiiEntityType.Email:
            {
                v = PiiRegex.Whitespace.Replace(v, "");
                v = v.Replace("(at)", "@", StringComparison.OrdinalIgnoreCase)
                     .Replace("[at]", "@", StringComparison.OrdinalIgnoreCase)
                     .Replace("(dot)", ".", StringComparison.OrdinalIgnoreCase)
                     .Replace("[dot]", ".", StringComparison.OrdinalIgnoreCase);
                return v.ToLowerInvariant();
            }
            case PiiEntityType.Person:
            {
                v = PiiRegex.LeadingTitle.Replace(v, "");
                return PiiRegex.Whitespace.Replace(v, " ").Trim().ToLowerInvariant();
            }
            default:
                return PiiRegex.Whitespace.Replace(v, " ").Trim().ToLowerInvariant();
        }
    }
}
