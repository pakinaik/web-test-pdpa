namespace WebTestPdpa.Api.Contracts;

public sealed record LoginRequest(string? UserName, string? Password);

public sealed record UserInfo(Guid Id, string UserName, string DisplayName, IReadOnlyList<string> Roles);
