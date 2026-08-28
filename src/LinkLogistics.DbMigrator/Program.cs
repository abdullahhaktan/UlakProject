using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace LinkLogistics.DbMigrator;

/// <summary>
/// Console entry point for docker-compose: waits for SQL Server, then applies
/// the embedded scripts via <see cref="Migrator"/>.
/// </summary>
internal static class Program
{
    private static int Main()
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString =
            config.GetConnectionString("Default")
            ?? config["ConnectionStrings:Default"]
            ?? throw new InvalidOperationException("ConnectionStrings:Default is not configured.");

        var retryCount = config.GetValue("Migrator:ConnectRetryCount", 30);
        var retryDelay = TimeSpan.FromSeconds(config.GetValue("Migrator:ConnectRetryDelaySeconds", 3));

        Console.WriteLine($"[db-migrator] target: {Redact(connectionString)}");

        if (!Migrator.WaitForSqlServer(connectionString, retryCount, retryDelay))
        {
            Console.Error.WriteLine("[db-migrator] SQL Server did not become available in time.");
            return 2;
        }

        var result = Migrator.Run(connectionString);
        return result.Successful ? 0 : 1;
    }

    private static string Redact(string connectionString)
    {
        var builder = new SqlConnectionStringBuilder(connectionString);
        if (!string.IsNullOrEmpty(builder.Password))
        {
            builder.Password = "***";
        }

        return builder.ConnectionString;
    }
}
