using System.Data;
using Ulak.Core.Abstractions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace Ulak.Infrastructure.Persistence;

public sealed class SqlConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    public SqlConnectionFactory(IOptions<DatabaseOptions> options)
    {
        _connectionString = options.Value.ConnectionString;
    }

    public IDbConnection Create() => new SqlConnection(_connectionString);
}
