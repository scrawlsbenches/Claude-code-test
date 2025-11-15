using HotSwap.Distributed.Api.Validation;
using System.Net;
using System.Text.Json;

namespace HotSwap.Distributed.Api.Middleware;

/// <summary>
/// Middleware for global exception handling and consistent error responses
/// </summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
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
        var response = context.Response;
        response.ContentType = "application/json";

        var errorResponse = new ErrorResponse
        {
            TraceId = context.TraceIdentifier,
            Timestamp = DateTimeOffset.UtcNow
        };

        switch (exception)
        {
            case ValidationException validationEx:
                response.StatusCode = (int)HttpStatusCode.BadRequest;
                errorResponse.Error = "Validation Failed";
                errorResponse.Message = "One or more validation errors occurred";
                errorResponse.Details = validationEx.Errors;

                _logger.LogWarning(
                    validationEx,
                    "Validation failed for request {Path}. Errors: {Errors}",
                    context.Request.Path,
                    string.Join(", ", validationEx.Errors));
                break;

            case ArgumentNullException argNullEx:
                response.StatusCode = (int)HttpStatusCode.BadRequest;
                errorResponse.Error = "Bad Request";
                errorResponse.Message = argNullEx.Message;

                _logger.LogWarning(
                    argNullEx,
                    "ArgumentNullException for request {Path}: {Message}",
                    context.Request.Path,
                    argNullEx.Message);
                break;

            case ArgumentException argEx:
                response.StatusCode = (int)HttpStatusCode.BadRequest;
                errorResponse.Error = "Bad Request";
                errorResponse.Message = argEx.Message;

                _logger.LogWarning(
                    argEx,
                    "ArgumentException for request {Path}: {Message}",
                    context.Request.Path,
                    argEx.Message);
                break;

            case KeyNotFoundException keyNotFoundEx:
                response.StatusCode = (int)HttpStatusCode.NotFound;
                errorResponse.Error = "Not Found";
                errorResponse.Message = keyNotFoundEx.Message;

                _logger.LogWarning(
                    keyNotFoundEx,
                    "Resource not found for request {Path}: {Message}",
                    context.Request.Path,
                    keyNotFoundEx.Message);
                break;

            case UnauthorizedAccessException unauthorizedEx:
                response.StatusCode = (int)HttpStatusCode.Unauthorized;
                errorResponse.Error = "Unauthorized";
                errorResponse.Message = "Authentication required";

                _logger.LogWarning(
                    unauthorizedEx,
                    "Unauthorized access attempt for request {Path}",
                    context.Request.Path);
                break;

            case InvalidOperationException invalidOpEx:
                response.StatusCode = (int)HttpStatusCode.Conflict;
                errorResponse.Error = "Conflict";
                errorResponse.Message = invalidOpEx.Message;

                _logger.LogWarning(
                    invalidOpEx,
                    "Invalid operation for request {Path}: {Message}",
                    context.Request.Path,
                    invalidOpEx.Message);
                break;

            case TimeoutException timeoutEx:
                response.StatusCode = (int)HttpStatusCode.RequestTimeout;
                errorResponse.Error = "Request Timeout";
                errorResponse.Message = "The request took too long to process";

                _logger.LogError(
                    timeoutEx,
                    "Request timeout for {Path}: {Message}",
                    context.Request.Path,
                    timeoutEx.Message);
                break;

            default:
                response.StatusCode = (int)HttpStatusCode.InternalServerError;
                errorResponse.Error = "Internal Server Error";
                errorResponse.Message = "An unexpected error occurred";

                // Only include exception details in development
                if (_environment.IsDevelopment())
                {
                    errorResponse.Details = new List<string>
                    {
                        exception.Message,
                        exception.StackTrace ?? "No stack trace available"
                    };
                }

                _logger.LogError(
                    exception,
                    "Unhandled exception for request {Path}: {Message}",
                    context.Request.Path,
                    exception.Message);
                break;
        }

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        var jsonResponse = JsonSerializer.Serialize(errorResponse, options);
        await response.WriteAsync(jsonResponse);
    }
}

/// <summary>
/// Standard error response model
/// </summary>
public class ErrorResponse
{
    /// <summary>
    /// Error type or category
    /// </summary>
    public string Error { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable error message
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Trace identifier for correlation
    /// </summary>
    public string TraceId { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp of the error
    /// </summary>
    public DateTimeOffset Timestamp { get; set; }

    /// <summary>
    /// Additional error details (validation errors, stack traces, etc.)
    /// </summary>
    public List<string>? Details { get; set; }
}
