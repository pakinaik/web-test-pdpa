namespace WebTestPdpa.Api.Pii;

public sealed record GuardResult(bool Passed, IReadOnlyList<string> Reasons);

/// <summary>🔍 AL8: guard ชั้นสุดท้ายก่อนส่ง LLM — scan ข้อความ masked ซ้ำด้วย regex</summary>
public sealed class OutboundGuard
{
    public GuardResult Check(IEnumerable<string> contents)
    {
        var reasons = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var content in contents)
        {
            // 🔄 ลบ token ที่ถูกต้องออก แล้ว normalize (เลขไทย/zero-width)
            var stripped = PiiRegex.Token.Replace(content, " ");
            var text = TextNormalizer.Normalize(stripped);

            if (PiiRegex.ThaiIdLike.IsMatch(text)) reasons.Add("THAI_ID_LIKE");
            if (PiiRegex.PhoneMobile.IsMatch(text) || PiiRegex.PhoneLandline.IsMatch(text)) reasons.Add("PHONE_LIKE");
            if (PiiRegex.EmailLike.IsMatch(text)) reasons.Add("EMAIL_LIKE");
        }
        return new GuardResult(reasons.Count == 0, reasons.ToList());
    }
}
