using Ulak.DbMigrator;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;

namespace Ulak.IntegrationTests;

/// <summary>
/// Boots the real API against a throwaway SQL Server container with the
/// production migration scripts + seed data applied.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly MsSqlContainer _sql =
        new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    private string _connectionString = string.Empty;

    public async Task InitializeAsync()
    {
        await _sql.StartAsync();

        _connectionString = new SqlConnectionStringBuilder(_sql.GetConnectionString())
        {
            InitialCatalog = "Ulak_IT",
            TrustServerCertificate = true,
        }.ConnectionString;

        var result = Migrator.Run(_connectionString);
        if (!result.Successful)
        {
            throw new InvalidOperationException($"Migration failed: {result.Error}");
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:Default", _connectionString);
        builder.UseSetting("Jwt:SigningKey", "integration-tests-signing-key-please-32-plus-chars");
        builder.UseSetting("Jwt:Issuer", "ulak");
        builder.UseSetting("Jwt:Audience", "ulak");
        builder.UseSetting("Storage:Endpoint", "http://localhost:9000");
        builder.UseSetting("Storage:PublicEndpoint", "http://localhost:9000");
        builder.UseSetting("Storage:AccessKey", "integration");
        builder.UseSetting("Storage:SecretKey", "integration-secret");
        builder.UseSetting("Storage:Bucket", "proofs");
    }

    public new async Task DisposeAsync()
    {
        await _sql.DisposeAsync();
        await base.DisposeAsync();
    }
}

[CollectionDefinition(nameof(ApiCollection))]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>;
