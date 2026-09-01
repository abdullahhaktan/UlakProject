using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace Ulak.Api.Infrastructure;

/// <summary>
/// Turns the <c>THROW 50xxx</c> errors raised by the stored procedures into
/// clean RFC 7807 responses instead of a 500. Everything else falls through
/// to the framework handler.
/// </summary>
public sealed class SqlErrorExceptionHandler : IExceptionHandler
{
    // SP error number -> HTTP status
    private static readonly IReadOnlyDictionary<int, int> Map = new Dictionary<int, int>
    {
        [50010] = StatusCodes.Status409Conflict,   // duplicate order ref
        [50011] = StatusCodes.Status400BadRequest, // invalid driver
        [50012] = StatusCodes.Status404NotFound,   // delivery not found
        [50013] = StatusCodes.Status409Conflict,   // delivery not pending
        [50020] = StatusCodes.Status404NotFound,   // delivery not found (proof)
        [50021] = StatusCodes.Status403Forbidden,  // delivery not assigned to this driver
        [50022] = StatusCodes.Status409Conflict,   // delivery already has a proof
        [50023] = StatusCodes.Status400BadRequest, // failure reason required
        [50024] = StatusCodes.Status400BadRequest, // too many photos
        [50030] = StatusCodes.Status409Conflict,   // sign-up phone already registered
        [50031] = StatusCodes.Status409Conflict,   // create-driver phone already registered
    };

    private readonly ILogger<SqlErrorExceptionHandler> _logger;

    public SqlErrorExceptionHandler(ILogger<SqlErrorExceptionHandler> logger) => _logger = logger;

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not SqlException sql || !Map.TryGetValue(sql.Number, out var status))
        {
            return false;
        }

        _logger.LogInformation("Mapped SQL error {Number} to HTTP {Status}: {Message}",
            sql.Number, status, sql.Message);

        var problem = new ProblemDetails
        {
            Status = status,
            Title = ReasonPhrases.For(status),
            Detail = sql.Message,
            Type = $"https://httpstatuses.io/{status}",
        };

        httpContext.Response.StatusCode = status;
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
        return true;
    }

    private static class ReasonPhrases
    {
        public static string For(int status) => status switch
        {
            400 => "Bad Request",
            403 => "Forbidden",
            404 => "Not Found",
            409 => "Conflict",
            _ => "Error",
        };
    }
}
