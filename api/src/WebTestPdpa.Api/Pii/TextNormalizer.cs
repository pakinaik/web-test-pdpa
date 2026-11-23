using System.Text;

namespace WebTestPdpa.Api.Pii;

/// <summary>🔄 AL1: เลขไทย/full-width -> 0-9 และลบ zero-width (logic เดียวกับ pii/app/normalize.py)</summary>
public static class TextNormalizer
{
    public static string Normalize(string text)
    {
        // 🔄 สร้างข้อความใหม่โดยแปลงเลขและตัด zero-width
        var sb = new StringBuilder(text.Length);
        foreach (var ch in text)
        {
            switch (ch)
            {
                case '\u200B':
                case '\u200C':
                case '\u200D':
                case '\u2060':
                case '\uFEFF':
                    continue;
                case >= '\u0E50' and <= '\u0E59':
                    sb.Append((char)(ch - '\u0E50' + '0'));
                    break;
                case >= '\uFF10' and <= '\uFF19':
                    sb.Append((char)(ch - '\uFF10' + '0'));
                    break;
                default:
                    // 🔄 whitespace ทุกชนิด (NBSP, tab, newline ฯลฯ) -> ช่องว่างปกติ แบบ 1:1 (logic เดียวกับ pii)
                    // ⚠️ N3: Python str.isspace() นับ U+001C-U+001F แต่ char.IsWhiteSpace ไม่นับ จึงเพิ่มให้ตรงกัน
                    sb.Append(char.IsWhiteSpace(ch) || (ch >= '\u001C' && ch <= '\u001F') ? ' ' : ch);
                    break;
            }
        }
        return sb.ToString();
    }
}
