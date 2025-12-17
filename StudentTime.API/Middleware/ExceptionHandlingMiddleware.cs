using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StudentTime.Core.Exceptions;

namespace StudentTime.API.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        // Ajouter le header COOP pour permettre Google OAuth popup même en cas d'erreur
        if (!context.Response.Headers.ContainsKey("Cross-Origin-Opener-Policy"))
        {
            context.Response.Headers.Append("Cross-Origin-Opener-Policy", "same-origin-allow-popups");
        }
        
        context.Response.ContentType = "application/json";

        var response = exception switch
        {
            BusinessException businessEx => new
            {
                statusCode = (int)HttpStatusCode.BadRequest,
                message = businessEx.Message
            },
            NotFoundException notFoundEx => new
            {
                statusCode = (int)HttpStatusCode.NotFound,
                message = notFoundEx.Message
            },
            UnauthorizedAccessException => new
            {
                statusCode = (int)HttpStatusCode.Unauthorized,
                message = "Accès non autorisé"
            },
            ArgumentNullException argNullEx => new
            {
                statusCode = (int)HttpStatusCode.BadRequest,
                message = argNullEx.Message ?? "Un paramètre requis est manquant"
            },
            ArgumentException argEx => new
            {
                statusCode = (int)HttpStatusCode.BadRequest,
                message = argEx.Message
            },
            DbUpdateException dbEx => new
            {
                statusCode = (int)HttpStatusCode.BadRequest,
                message = "Erreur lors de la sauvegarde des données. Veuillez réessayer."
            },
            _ => new
            {
                statusCode = (int)HttpStatusCode.InternalServerError,
                message = "Une erreur est survenue. Notre équipe a été notifiée."
            }
        };

        context.Response.StatusCode = response.statusCode;

        // Logger toutes les exceptions pour diagnostic
        _logger.LogError(exception, "Exception capturée - Type: {Type}, Message: {Message}", 
            exception.GetType().Name, exception.Message);
        _logger.LogError(exception, "Stack trace: {StackTrace}", exception.StackTrace);
        
        if (exception.InnerException != null)
        {
            _logger.LogError(exception.InnerException, "Inner exception - Type: {Type}, Message: {Message}", 
                exception.InnerException.GetType().Name, exception.InnerException.Message);
            _logger.LogError(exception.InnerException, "Inner exception stack trace: {StackTrace}", 
                exception.InnerException.StackTrace);
        }
        
        if (response.statusCode == 500)
        {
            _logger.LogError("⚠️ Erreur 500 - Exception non gérée de type {Type}", exception.GetType().Name);
        }

        await context.Response.WriteAsync(JsonSerializer.Serialize(response));
    }
}

