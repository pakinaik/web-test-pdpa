using System.Security.Cryptography;
using System.Text;
using WebTestPdpa.Api.Reports;

namespace WebTestPdpa.Api.Tests.Reports;

public class ReportCryptoTests
{
    private static ReportCrypto New() => new(RandomNumberGenerator.GetBytes(32));

    [Fact]
    public void RoundTrip()
    {
        var c = New();
        var id = Guid.NewGuid();
        var user = Guid.NewGuid();
        var plain = Encoding.UTF8.GetBytes("%PDF-1.7 ทดสอบ");
        var data = c.Encrypt(plain, id, user);
        Assert.NotEqual(plain, data[12..^16]);
        Assert.Equal(plain, c.Decrypt(data, id, user));
    }

    [Fact]
    public void WrongUser_Throws()
    {
        var c = New();
        var id = Guid.NewGuid();
        var data = c.Encrypt([1, 2, 3], id, Guid.NewGuid());
        Assert.ThrowsAny<CryptographicException>(() => c.Decrypt(data, id, Guid.NewGuid()));
    }

    [Fact]
    public void Tampered_Throws()
    {
        var c = New();
        var id = Guid.NewGuid();
        var user = Guid.NewGuid();
        var data = c.Encrypt([1, 2, 3], id, user);
        data[13] ^= 0xFF;
        Assert.ThrowsAny<CryptographicException>(() => c.Decrypt(data, id, user));
    }

    [Fact]
    public void KeyDiffersFromVault()
    {
        // 🔍 key ของรายงานต้อง derive แยกจาก vault (master เดียวกัน decrypt ข้ามกันไม่ได้)
        var master = RandomNumberGenerator.GetBytes(32);
        var report = new ReportCrypto(master);
        var vault = new WebTestPdpa.Api.Pii.VaultCrypto(master);
        var id = Guid.NewGuid();
        var data = vault.Encrypt("x", id, "[T_1]");
        Assert.ThrowsAny<CryptographicException>(() => report.Decrypt(data, id, id));
    }

    [Fact]
    public void BadKey_Throws()
    {
        Assert.Throws<ArgumentException>(() => new ReportCrypto(new byte[16]));
        Assert.Throws<ArgumentException>(() => ReportCrypto.FromBase64("not-base64!"));
        Assert.Throws<ArgumentException>(() => ReportCrypto.FromBase64(""));
    }
}

public class ReportIntentTests
{
    [Theory]
    [InlineData("ขอรายงานสรุปรายได้ เป็น pdf ให้โหลดหน่อย", true)]
    [InlineData("ทำเป็น PDF ได้มั้ย", true)]
    [InlineData("ขอเป็นไฟล์พีดีเอฟ", true)]
    [InlineData("ขอรายงานสรุปรายได้ทั้งปี", false)]
    [InlineData("", false)]
    public void WantsPdf(string text, bool expected) => Assert.Equal(expected, ReportIntent.WantsPdf(text));
}

public class ReportMarkdownTests
{
    [Fact]
    public void Parse_HeadingTableBulletParagraph()
    {
        var md = "# สรุปรายได้\n\nย่อหน้า **หนา**\n\n| เดือน | จำนวน |\n|---|---:|\n| ม.ค. | 1,000 |\n| ก.พ. | 2,000 |\n\n- รวม 3,000\n```\ncode\n```";
        var blocks = ReportMarkdown.Parse(md);

        Assert.Equal(new ReportMarkdown.Heading(1, "สรุปรายได้"), blocks[0]);
        Assert.Equal(new ReportMarkdown.Paragraph("ย่อหน้า หนา"), blocks[1]);
        var table = Assert.IsType<ReportMarkdown.Table>(blocks[2]);
        Assert.Equal(3, table.Rows.Count);
        Assert.Equal(["เดือน", "จำนวน"], table.Rows[0]);
        Assert.Equal(["ก.พ.", "2,000"], table.Rows[2]);
        Assert.Equal(new ReportMarkdown.Bullet("รวม 3,000"), blocks[3]);
        // 🔍 code fence ถูกข้าม แต่เนื้อในเป็นย่อหน้าธรรมดา
        Assert.Equal(new ReportMarkdown.Paragraph("code"), blocks[4]);
        Assert.Equal(5, blocks.Count);
    }

    [Fact]
    public void Parse_Empty() => Assert.Empty(ReportMarkdown.Parse(null));
}

public class QuestPdfReportRendererTests
{
    [Fact]
    public void Render_ProducesPdf()
    {
        var r = new QuestPdfReportRenderer();
        var pdf = r.Render(new ReportDocument("รายงาน", "emp020", DateTimeOffset.UtcNow,
            "# หัวข้อ\n| ประเภท | รวม |\n|---|---|\n| เงินเดือน | 600,000 |\n- รายการ ที่ ๑", "ข้อความเตือน"));
        Assert.True(pdf.Length > 1000);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(pdf, 0, 4));
    }
}
