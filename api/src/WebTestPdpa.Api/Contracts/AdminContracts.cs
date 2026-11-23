namespace WebTestPdpa.Api.Contracts;

public sealed record CreateUserRequest(
    string? UserName, string? Password, string? FirstNameTh, string? LastNameTh,
    string? FirstNameEn, string? LastNameEn, string? Role);
