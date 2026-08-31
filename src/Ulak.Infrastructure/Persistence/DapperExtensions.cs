using System.Data;
using Dapper;

namespace Ulak.Infrastructure.Persistence;

/// <summary>
/// Thin helpers so every repository call goes through a stored procedure
/// with <see cref="CommandType.StoredProcedure"/> and a cancellation token.
/// </summary>
internal static class DapperExtensions
{
    public static Task<IEnumerable<T>> QueryProcAsync<T>(
        this IDbConnection connection, string proc, object? param, CancellationToken ct) =>
        connection.QueryAsync<T>(new CommandDefinition(
            proc, param, commandType: CommandType.StoredProcedure, cancellationToken: ct));

    public static Task<T?> QuerySingleOrDefaultProcAsync<T>(
        this IDbConnection connection, string proc, object? param, CancellationToken ct) =>
        connection.QuerySingleOrDefaultAsync<T?>(new CommandDefinition(
            proc, param, commandType: CommandType.StoredProcedure, cancellationToken: ct));

    public static Task<int> ExecuteProcAsync(
        this IDbConnection connection, string proc, object? param, CancellationToken ct) =>
        connection.ExecuteAsync(new CommandDefinition(
            proc, param, commandType: CommandType.StoredProcedure, cancellationToken: ct));

    public static Task<SqlMapper.GridReader> QueryMultipleProcAsync(
        this IDbConnection connection, string proc, object? param, CancellationToken ct) =>
        connection.QueryMultipleAsync(new CommandDefinition(
            proc, param, commandType: CommandType.StoredProcedure, cancellationToken: ct));
}
