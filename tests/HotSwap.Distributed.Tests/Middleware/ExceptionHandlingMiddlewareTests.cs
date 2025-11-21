using System.Net;
using System.Text.Json;
using FluentAssertions;
using HotSwap.Distributed.Api.Middleware;
using HotSwap.Distributed.Api.Validation;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace HotSwap.Distributed.Tests.Middleware;

public class ExceptionHandlingMiddlewareTests
{
    private readonly Mock<ILogger<ExceptionHandlingMiddleware>> _loggerMock;
    private readonly Mock<RequestDelegate> _nextMock;
    private readonly Mock<IHostEnvironment> _environmentMock;
    private DefaultHttpContext _httpContext;

    public ExceptionHandlingMiddlewareTests()
    {
        _loggerMock = new Mock<ILogger<ExceptionHandlingMiddleware>>();
        _nextMock = new Mock<RequestDelegate>();
        _environmentMock = new Mock<IHostEnvironment>();
        _httpContext = new DefaultHttpContext
        {
            Response = { Body = new MemoryStream() },
            TraceIdentifier = "test-trace-id-12345"
        };
        _httpContext.Request.Path = "/api/v1/test";
    }

    [Fact]
    public async Task InvokeAsync_WithValidationException_ShouldReturn400WithErrorDetails()
    {
        // Arrange
        var errors = new List<string> { "ModuleName is required", "Version must be semantic version" };
        var validationException = new ValidationException(errors);

        _nextMock
            .Setup(next => next(_httpContext))
            .ThrowsAsync(validationException);

        _environmentMock.Setup(e => e.EnvironmentName).Returns(Environments.Production);

        var middleware = new ExceptionHandlingMiddleware(
            _nextMock.Object,
            _loggerMock.Object,
            _environmentMock.Object);

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        _httpContext.Response.StatusCode.Should().Be((int)HttpStatusCode.BadRequest);
        _httpContext.Response.ContentType.Should().Be("application/json");

        _httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        var responseBody = await new StreamReader(_httpContext.Response.Body).ReadToEndAsync();
        var errorResponse = JsonSerializer.Deserialize<ErrorResponse>(responseBody, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        errorResponse.Should().NotBeNull();
        errorResponse!.Error.Should().Be("Validation Failed");
        errorResponse.Message.Should().Be("One or more validation errors occurred");
        errorResponse.TraceId.Should().Be("test-trace-id-12345");
        errorResponse.Details.Should().BeEquivalentTo(errors);
        errorResponse.Timestamp.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Validation failed")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_WithArgumentNullException_ShouldReturn400()
    {
        // Arrange
        var exception = new ArgumentNullException("moduleName", "Module name cannot be null");

        _nextMock
            .Setup(next => next(_httpContext))
            .ThrowsAsync(exception);

        _environmentMock.Setup(e => e.EnvironmentName).Returns(Environments.Production);

        var middleware = new ExceptionHandlingMiddleware(
            _nextMock.Object,
            _loggerMock.Object,
            _environmentMock.Object);

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        _httpContext.Response.StatusCode.Should().Be((int)HttpStatusCode.BadRequest);
        _httpContext.Response.ContentType.Should().Be("application/json");

        _httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        var responseBody = await new StreamReader(_httpContext.Response.Body).ReadToEndAsync();
        var errorResponse = JsonSerializer.Deserialize<ErrorResponse>(responseBody, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        errorResponse.Should().NotBeNull();
        errorResponse!.Error.Should().Be("Bad Request");
        errorResponse.Message.Should().Contain("Module name cannot be null");
        errorResponse.TraceId.Should().Be("test-trace-id-12345");

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("ArgumentNullException")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_WithArgumentException_ShouldReturn400()
    {
        // Arrange
        var exception = new ArgumentException("Invalid version format");

        _nextMock
            .Setup(next => next(_httpContext))
            .ThrowsAsync(exception);

        _environmentMock.Setup(e => e.EnvironmentName).Returns(Environments.Production);

        var middleware = new ExceptionHandlingMiddleware(
            _nextMock.Object,
            _loggerMock.Object,
            _environmentMock.Object);

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        _httpContext.Response.StatusCode.Should().Be((int)HttpStatusCode.BadRequest);
        _httpContext.Response.ContentType.Should().Be("application/json");

        _httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        var responseBody = await new StreamReader(_httpContext.Response.Body).ReadToEndAsync();
        var errorResponse = JsonSerializer.Deserialize<ErrorResponse>(responseBody, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        errorResponse.Should().NotBeNull();
        errorResponse!.Error.Should().Be("Bad Request");
        errorResponse.Message.Should().Be("Invalid version format");
        errorResponse.TraceId.Should().Be("test-trace-id-12345");
    }

    [Fact]
    public async Task InvokeAsync_WithKeyNotFoundException_ShouldReturn404()
    {
        // Arrange
        var exception = new KeyNotFoundException("Deployment not found");

        _nextMock
            .Setup(next => next(_httpContext))
            .ThrowsAsync(exception);

        _environmentMock.Setup(e => e.EnvironmentName).Returns(Environments.Production);

        var middleware = new ExceptionHandlingMiddleware(
            _nextMock.Object,
            _loggerMock.Object,
            _environmentMock.Object);

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        _httpContext.Response.StatusCode.Should().Be((int)HttpStatusCode.NotFound);
        _httpContext.Response.ContentType.Should().Be("application/json");

        _httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        var responseBody = await new StreamReader(_httpContext.Response.Body).ReadToEndAsync();
        var errorResponse = JsonSerializer.Deserialize<ErrorResponse>(responseBody, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        errorResponse.Should().NotBeNull();
        errorResponse!.Error.Should().Be("Not Found");
        errorResponse.Message.Should().Be("Deployment not found");
        errorResponse.TraceId.Should().Be("test-trace-id-12345");

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Resource not found")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_WithUnauthorizedAccessException_ShouldReturn401()
    {
        // Arrange
        var exception = new UnauthorizedAccessException("User not authenticated");

        _nextMock
            .Setup(next => next(_httpContext))
            .ThrowsAsync(exception);

        _environmentMock.Setup(e => e.EnvironmentName).Returns(Environments.Production);

        var middleware = new ExceptionHandlingMiddleware(
            _nextMock.Object,
            _loggerMock.Object,
            _environmentMock.Object);

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        _httpContext.Response.StatusCode.Should().Be((int)HttpStatusCode.Unauthorized);
        _httpContext.Response.ContentType.Should().Be("application/json");

        _httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        var responseBody = await new StreamReader(_httpContext.Response.Body).ReadToEndAsync();
        var errorResponse = JsonSerializer.Deserialize<ErrorResponse>(responseBody, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        errorResponse.Should().NotBeNull();
        errorResponse!.Error.Should().Be("Unauthorized");
        errorResponse.Message.Should().Be("Authentication required");
        errorResponse.TraceId.Should().Be("test-trace-id-12345");

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Unauthorized access attempt")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_WithInvalidOperationException_ShouldReturn409()
    {
        // Arrange
        var exception = new InvalidOperationException("Deployment already in progress");

        _nextMock
            .Setup(next => next(_httpContext))
            .ThrowsAsync(exception);

        _environmentMock.Setup(e => e.EnvironmentName).Returns(Environments.Production);

        var middleware = new ExceptionHandlingMiddleware(
            _nextMock.Object,
            _loggerMock.Object,
            _environmentMock.Object);

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        _httpContext.Response.StatusCode.Should().Be((int)HttpStatusCode.Conflict);
        _httpContext.Response.ContentType.Should().Be("application/json");

        _httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        var responseBody = await new StreamReader(_httpContext.Response.Body).ReadToEndAsync();
        var errorResponse = JsonSerializer.Deserialize<ErrorResponse>(responseBody, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        errorResponse.Should().NotBeNull();
        errorResponse!.Error.Should().Be("Conflict");
        errorResponse.Message.Should().Be("Deployment already in progress");
        errorResponse.TraceId.Should().Be("test-trace-id-12345");

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Invalid operation")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_WithTimeoutException_ShouldReturn408()
    {
        // Arrange
        var exception = new TimeoutException("Request processing timeout");

        _nextMock
            .Setup(next => next(_httpContext))
            .ThrowsAsync(exception);

        _environmentMock.Setup(e => e.EnvironmentName).Returns(Environments.Production);

        var middleware = new ExceptionHandlingMiddleware(
            _nextMock.Object,
            _loggerMock.Object,
            _environmentMock.Object);

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        _httpContext.Response.StatusCode.Should().Be((int)HttpStatusCode.RequestTimeout);
        _httpContext.Response.ContentType.Should().Be("application/json");

        _httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        var responseBody = await new StreamReader(_httpContext.Response.Body).ReadToEndAsync();
        var errorResponse = JsonSerializer.Deserialize<ErrorResponse>(responseBody, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        errorResponse.Should().NotBeNull();
        errorResponse!.Error.Should().Be("Request Timeout");
        errorResponse.Message.Should().Be("The request took too long to process");
        errorResponse.TraceId.Should().Be("test-trace-id-12345");

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Request timeout")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_WithUnhandledExceptionInDevelopment_ShouldReturn500WithStackTrace()
    {
        // Arrange
        var exception = new InvalidCastException("Cannot cast type A to type B");

        _nextMock
            .Setup(next => next(_httpContext))
            .ThrowsAsync(exception);

        _environmentMock.Setup(e => e.EnvironmentName).Returns(Environments.Development);

        var middleware = new ExceptionHandlingMiddleware(
            _nextMock.Object,
            _loggerMock.Object,
            _environmentMock.Object);

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        _httpContext.Response.StatusCode.Should().Be((int)HttpStatusCode.InternalServerError);
        _httpContext.Response.ContentType.Should().Be("application/json");

        _httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        var responseBody = await new StreamReader(_httpContext.Response.Body).ReadToEndAsync();
        var errorResponse = JsonSerializer.Deserialize<ErrorResponse>(responseBody, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        errorResponse.Should().NotBeNull();
        errorResponse!.Error.Should().Be("Internal Server Error");
        errorResponse.Message.Should().Be("An unexpected error occurred");
        errorResponse.TraceId.Should().Be("test-trace-id-12345");
        errorResponse.Details.Should().NotBeNull();
        errorResponse.Details.Should().HaveCountGreaterThan(0);
        errorResponse.Details![0].Should().Contain("Cannot cast type A to type B");

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Unhandled exception")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_WithUnhandledExceptionInProduction_ShouldReturn500WithoutStackTrace()
    {
        // Arrange
        var exception = new InvalidCastException("Cannot cast type A to type B");

        _nextMock
            .Setup(next => next(_httpContext))
            .ThrowsAsync(exception);

        _environmentMock.Setup(e => e.EnvironmentName).Returns(Environments.Production);

        var middleware = new ExceptionHandlingMiddleware(
            _nextMock.Object,
            _loggerMock.Object,
            _environmentMock.Object);

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        _httpContext.Response.StatusCode.Should().Be((int)HttpStatusCode.InternalServerError);
        _httpContext.Response.ContentType.Should().Be("application/json");

        _httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        var responseBody = await new StreamReader(_httpContext.Response.Body).ReadToEndAsync();
        var errorResponse = JsonSerializer.Deserialize<ErrorResponse>(responseBody, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        errorResponse.Should().NotBeNull();
        errorResponse!.Error.Should().Be("Internal Server Error");
        errorResponse.Message.Should().Be("An unexpected error occurred");
        errorResponse.TraceId.Should().Be("test-trace-id-12345");
        errorResponse.Details.Should().BeNullOrEmpty(); // No stack trace in production

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Unhandled exception")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_WhenNoExceptionThrown_ShouldCallNextMiddleware()
    {
        // Arrange
        _nextMock
            .Setup(next => next(_httpContext))
            .Returns(Task.CompletedTask);

        _environmentMock.Setup(e => e.EnvironmentName).Returns(Environments.Production);

        var middleware = new ExceptionHandlingMiddleware(
            _nextMock.Object,
            _loggerMock.Object,
            _environmentMock.Object);

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        _nextMock.Verify(next => next(_httpContext), Times.Once);
        _httpContext.Response.StatusCode.Should().Be(200); // Default OK status

        _loggerMock.Verify(
            x => x.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never); // No logging when no exception
    }
}
