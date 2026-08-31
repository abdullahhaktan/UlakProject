using System.Reflection;
using DbUp;
using DbUp.Engine;
using Microsoft.Data.SqlClient;

namespace Ulak.DbMigrator;

/// <summary>
/// Applies the embedded <c>db/scripts/*.sql</c> files once each, journaled in
/// <c>dbo.SchemaVersions</c>. Reused by the console entry point and the
/// integration tests.
/// </summary>
public static class Migrator
{
    private const string ScriptPrefix = "Ulak.DbMigrator.Scripts.";

    public static DatabaseUpgradeResult Run(string connectionString, Action<string>? log = null)
    {
        log ??= Console.WriteLine;

        EnsureDatabase.For.SqlDatabase(connectionString);

        UpgradeEngine upgrader = DeployChanges.To
            .SqlDatabase(connectionString)
            .WithScriptsEmbeddedInAssembly(
                typeof(Migrator).Assembly,
                name => name.StartsWith(ScriptPrefix, StringComparison.Ordinal))
            .WithTransactionPerScript()
            .WithVariablesDisabled()
            .LogToConsole()
            .JournalToSqlTable("dbo", "SchemaVersions")
            .Build();

        var result = upgrader.PerformUpgrade();
        log(result.Successful
            ? "[db-migrator] Success!"
            : $"[db-migrator] FAILED on '{result.ErrorScript?.Name}': {result.Error}");
        return result;
    }

    public static bool WaitForSqlServer(string connectionString, int attempts, TimeSpan delay, Action<string>? log = null)
    {
        log ??= Console.WriteLine;
        var builder = new SqlConnectionStringBuilder(connectionString)
        {
            InitialCatalog = "master",
            ConnectTimeout = 5,
        };

        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            try
            {
                using var connection = new SqlConnection(builder.ConnectionString);
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = "SELECT 1";
                command.ExecuteScalar();
                log($"[db-migrator] SQL Server reachable (attempt {attempt}).");
                return true;
            }
            catch (Exception ex) when (ex is SqlException or InvalidOperationException)
            {
                log($"[db-migrator] waiting for SQL Server ({attempt}/{attempts}): {ex.Message}");
                Thread.Sleep(delay);
            }
        }

        return false;
    }
}
