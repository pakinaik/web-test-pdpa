using WebTestPdpa.Api.Pii;

namespace WebTestPdpa.Api.Tests.Fakes;

/// <summary>💾 vault ในหน่วยความจำสำหรับ unit test</summary>
public sealed class InMemoryVaultStore : IVaultStore
{
    public List<VaultEntry> Entries { get; } = [];

    public Task<VaultEntry?> FindAsync(Guid conversationId, PiiEntityType type, byte[] valueHash, CancellationToken ct) =>
        Task.FromResult(Entries.FirstOrDefault(e =>
            e.ConversationId == conversationId && e.Type == type && e.ValueHash.AsSpan().SequenceEqual(valueHash)));

    public Task<IReadOnlyDictionary<PiiEntityType, int>> GetMaxIndexesAsync(Guid conversationId, CancellationToken ct)
    {
        IReadOnlyDictionary<PiiEntityType, int> r = Entries
            .Where(e => e.ConversationId == conversationId)
            .GroupBy(e => e.Type)
            .ToDictionary(g => g.Key, g => g.Max(x => x.TokenIndex));
        return Task.FromResult(r);
    }

    public Task AddAsync(VaultEntry entry, CancellationToken ct)
    {
        Entries.Add(entry);
        return Task.CompletedTask;
    }

    public Task TouchAsync(Guid entryId, CancellationToken ct)
    {
        var e = Entries.First(x => x.Id == entryId);
        e.LastUsedAt = DateTimeOffset.UtcNow;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<VaultEntry>> LoadAllAsync(Guid conversationId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<VaultEntry>>(Entries.Where(e => e.ConversationId == conversationId).ToList());
}
