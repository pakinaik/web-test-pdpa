using System.Text.RegularExpressions;
using WebTestPdpa.Api.Pii;

namespace WebTestPdpa.Api.Tests.Fakes;

/// <summary>🔄 fake pii: ตรวจเบอร์ (regex ง่าย) + known names (ordinal) ใช้ใน unit/integration test</summary>
public sealed class FakePiiDetectionClient : IPiiDetectionClient
{
    public bool Throw { get; set; }
    public bool NerEnabled { get; set; } = true;
    public List<IReadOnlyList<string>> SeenKnownNames { get; } = [];
    public Func<string, IReadOnlyList<DetectedEntity>>? Override { get; set; }

    private static readonly Regex Phone = new(@"0[689][0-9]-?[0-9]{3}-?[0-9]{4}", RegexOptions.None, TimeSpan.FromSeconds(1));
    private static readonly Regex Email = new(@"[a-z0-9._]+@[a-z0-9.]+\.[a-z]{2,}", RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));
    private static readonly Regex Id = new(@"(?<![0-9])1101700234568(?![0-9])", RegexOptions.None, TimeSpan.FromSeconds(1));

    public Task<PiiDetectionResult> DetectAsync(string text, IReadOnlyList<string> knownNames, CancellationToken ct)
    {
        if (Throw) throw new PiiUnavailableException("FAKE_DOWN");
        SeenKnownNames.Add(knownNames);
        if (Override is not null) return Task.FromResult(new PiiDetectionResult(Override(text), NerEnabled));

        var list = new List<DetectedEntity>();
        foreach (Match m in Phone.Matches(text)) list.Add(new(PiiEntityType.Phone, m.Index, m.Index + m.Length, "regex"));
        foreach (Match m in Email.Matches(text)) list.Add(new(PiiEntityType.Email, m.Index, m.Index + m.Length, "regex"));
        foreach (Match m in Id.Matches(text)) list.Add(new(PiiEntityType.ThaiId, m.Index, m.Index + m.Length, "checksum"));
        foreach (var n in knownNames)
        {
            var i = text.IndexOf(n, StringComparison.OrdinalIgnoreCase);
            while (i >= 0)
            {
                list.Add(new(PiiEntityType.Person, i, i + n.Length, "known_name"));
                i = text.IndexOf(n, i + n.Length, StringComparison.OrdinalIgnoreCase);
            }
        }
        return Task.FromResult(new PiiDetectionResult(list, NerEnabled));
    }

    public Task<PiiHealth?> GetHealthAsync(CancellationToken ct) =>
        Task.FromResult<PiiHealth?>(Throw ? null : new PiiHealth(NerEnabled));
}
