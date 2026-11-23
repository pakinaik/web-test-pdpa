using Npgsql;
using Testcontainers.PostgreSql;

namespace WebTestPdpa.Api.Tests.Integration;

/// <summary>🚀 Postgres 17 container เดียวต่อ collection; แต่ละ ApiFactory สร้าง database แยกเพื่อไม่ชนกัน</summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public async Task InitializeAsync() => await _container.StartAsync();

    public async Task DisposeAsync() => await _container.DisposeAsync();

    /// <summary>💾 สร้าง database ใหม่แล้วคืน connection string</summary>
    public async Task<string> CreateDatabaseAsync()
    {
        var name = "t_" + Guid.NewGuid().ToString("N");
        await using (var conn = new NpgsqlConnection(_container.GetConnectionString()))
        {
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand($"CREATE DATABASE \"{name}\"", conn);
            await cmd.ExecuteNonQueryAsync();
        }
        var b = new NpgsqlConnectionStringBuilder(_container.GetConnectionString()) { Database = name };
        return b.ConnectionString;
    }
}

[CollectionDefinition("pg")]
public class PgCollection : ICollectionFixture<PostgresFixture>;
