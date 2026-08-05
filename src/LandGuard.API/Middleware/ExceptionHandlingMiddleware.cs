using System.Net;
using System.Text.Json;
using FluentValidation;
using LandGuard.Domain.Exceptions;

namespace LandGuard.API.Middleware;

/// <summary>
/// Single, centralized place where exceptions become HTTP responses.
/// Without this, every controller action across Auth, Property, Fraud
/// Report and Admin modules would need its own try/catch, and the JSON
/// error shape returned to Buyer/Seller/Admin clients would drift between
/// endpoints as the API grows. Registered first in the middleware
/// pipeline (see Program.cs) so it wraps everything downstream, including
/// authentication/authorization and MVC model binding failures that
/// surface as thrown exceptions.
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

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Unhandled exception while processing {Method} {Path}",
                context.Request.Method,
                context.Request.Path);

            await WriteErrorResponseAsync(context, exception);
        }
    }

    private static Task WriteErrorResponseAsync(HttpContext context, Exception exception)
    {
        var (statusCode, title, errors) = MapException(exception);

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;

        var payload = JsonSerializer.Serialize(new
        {
            status = (int)statusCode,
            title,
            errors,
            traceId = context.TraceIdentifier
        });

        return context.Response.WriteAsync(payload);
    }

    private static (HttpStatusCode StatusCode, string Title, IEnumerable<string> Errors) MapException(Exception exception) =>
        exception switch
        {
            ValidationException validationException => (
                HttpStatusCode.BadRequest,
                "One or more validation errors occurred.",
                validationException.Errors.Select(e => e.ErrorMessage)),

            NotFoundException notFoundException => (
                HttpStatusCode.NotFound,
                notFoundException.Message,
                Enumerable.Empty<string>()),

            DomainException domainException => (
                HttpStatusCode.UnprocessableEntity,
                domainException.Message,
                Enumerable.Empty<string>()),

            UnauthorizedAccessException => (
                HttpStatusCode.Forbidden,
                "You are not authorized to perform this action.",
                Enumerable.Empty<string>()),

            _ => (
                HttpStatusCode.InternalServerError,
                "An unexpected error occurred. Please try again later.",
                Enumerable.Empty<string>())
        };
}
