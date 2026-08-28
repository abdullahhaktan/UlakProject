using System.Data;
using LinkLogistics.Core.Abstractions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace LinkLogistics.Infrastructure.Persistence;

public sealed class SqlConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    public SqlConnectionFactory(IOptions<DatabaseOptions> options)
    {
        _connectionString = options.Value.ConnectionString;
    }

    public IDbConnection Create() => new SqlConnection(_connectionString);
}
