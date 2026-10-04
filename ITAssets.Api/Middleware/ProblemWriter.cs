using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace ITAssets.Api.Middleware;

/// <summary>Escribe respuestas de error con formato uniforme (RFC 7807 ProblemDetails + 'code' y 'traceId').</summary>
public static class ProblemWriter
{
    public static Task WriteAsync(HttpContext ctx, int status, string code, string message)
    {
        if (ctx.Response.HasStarted) return Task.CompletedTask;

        ctx.Response.StatusCode = status;
        var problem = new ProblemDetails
        {
            Status = status,
            Title = ReasonPhrases.GetReasonPhrase(status),
            Detail = message
        };
        problem.Extensions["code"] = code;
        problem.Extensions["traceId"] = ctx.TraceIdentifier;
        return ctx.Response.WriteAsJsonAsync(problem, options: null, contentType: "application/problem+json");
    }
}
