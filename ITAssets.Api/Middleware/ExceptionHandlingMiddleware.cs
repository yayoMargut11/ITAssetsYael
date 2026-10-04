using System.Text.Json;
using Microsoft.Data.SqlClient;

namespace ITAssets.Api.Middleware;

/// <summary>
/// Manejo global de errores. Registra el detalle completo SOLO en el log del servidor y
/// devuelve al cliente un mensaje controlado: sin stack traces, sin mensajes de SQL, sin secretos.
/// </summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext ctx)
    {
        try
        {
            await _next(ctx);
        }
        catch (OperationCanceledException) when (ctx.RequestAborted.IsCancellationRequested)
        {
            // El cliente canceló la petición; no hay a quién responder.
        }
        catch (Exception ex)
        {
            if (ctx.Response.HasStarted)
            {
                _logger.LogError(ex, "Error después de iniciar la respuesta. TraceId {TraceId}", ctx.TraceIdentifier);
                throw;
            }
            await HandleAsync(ctx, ex);
        }
    }

    private async Task HandleAsync(HttpContext ctx, Exception ex)
    {
        switch (ex)
        {
            case AppException app:
                _logger.LogInformation("Error controlado {Code} ({Status}). TraceId {TraceId}", app.Code, app.StatusCode, ctx.TraceIdentifier);
                await ProblemWriter.WriteAsync(ctx, app.StatusCode, app.Code, app.Message);
                break;

            case SqlException sql when SqlErrorMapper.TryMap(sql.Number, out var info):
                _logger.LogInformation("Regla de negocio/BD {Code} (SQL {Number}). TraceId {TraceId}", info.Code, sql.Number, ctx.TraceIdentifier);
                await ProblemWriter.WriteAsync(ctx, info.Status, info.Code, info.Message);
                break;

            case SqlException sql:
                _logger.LogError(ex, "Error de base de datos (SQL {Number}). TraceId {TraceId}", sql.Number, ctx.TraceIdentifier);
                await ProblemWriter.WriteAsync(ctx, 500, "DATABASE_ERROR", "Ocurrió un error al procesar la solicitud. Inténtalo más tarde.");
                break;

            case BadHttpRequestException or JsonException:
                await ProblemWriter.WriteAsync(ctx, 400, "BAD_REQUEST", "La solicitud no tiene un formato válido.");
                break;

            default:
                _logger.LogError(ex, "Error no controlado. TraceId {TraceId}", ctx.TraceIdentifier);
                await ProblemWriter.WriteAsync(ctx, 500, "INTERNAL_ERROR", "Ocurrió un error inesperado. Inténtalo más tarde.");
                break;
        }
    }
}
