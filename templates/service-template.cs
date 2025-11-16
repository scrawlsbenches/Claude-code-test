using HotSwap.Distributed.Domain.Enums;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Infrastructure.Interfaces;
using Microsoft.Extensions.Logging;

namespace HotSwap.Distributed.{ProjectName}.Services;

/// <summary>
/// {ServiceDescription}
///
/// Responsibilities:
/// - {Responsibility1}
/// - {Responsibility2}
/// - {Responsibility3}
/// </summary>
public class {ServiceName} : I{ServiceName}
{
    // ============================================
    // Dependencies
    // ============================================
    private readonly ILogger<{ServiceName}> _logger;
    private readonly I{Dependency1} _{dependency1};
    private readonly I{Dependency2}? _{dependency2}; // Optional dependency
    private readonly {ConfigurationClass} _config;

    /// <summary>
    /// Initializes a new instance of the <see cref="{ServiceName}"/> class.
    /// </summary>
    /// <param name="logger">Logger instance for diagnostic output</param>
    /// <param name="{dependency1}">First required dependency</param>
    /// <param name="{dependency2}">Optional second dependency</param>
    /// <param name="config">Configuration for {ServiceName}</param>
    /// <exception cref="ArgumentNullException">Thrown when required parameter is null</exception>
    public {ServiceName}(
        ILogger<{ServiceName}> logger,
        I{Dependency1} {dependency1},
        {ConfigurationClass} config,
        I{Dependency2}? {dependency2} = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _{dependency1} = {dependency1} ?? throw new ArgumentNullException(nameof({dependency1}));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _{dependency2} = {dependency2}; // Optional, can be null
    }

    /// <summary>
    /// {MethodDescription}
    /// </summary>
    /// <param name="request">The request object containing input data</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>{ReturnDescription}</returns>
    /// <exception cref="ArgumentNullException">Thrown when request is null</exception>
    /// <exception cref="ArgumentException">Thrown when request contains invalid data</exception>
    /// <exception cref="InvalidOperationException">Thrown when operation cannot be performed</exception>
    public async Task<{ResultType}> {MethodName}Async(
        {RequestType} request,
        CancellationToken cancellationToken = default)
    {
        // ============================================
        // Input Validation
        // ============================================
        ArgumentNullException.ThrowIfNull(request, nameof(request));

        ValidateRequest(request);

        _logger.LogInformation(
            "{MethodName} called with request ID: {RequestId}",
            nameof({MethodName}Async),
            request.Id);

        try
        {
            // ============================================
            // Business Logic
            // ============================================

            // Step 1: Prepare data
            var preparedData = PrepareData(request);
            _logger.LogDebug("Data prepared for request {RequestId}", request.Id);

            // Step 2: Execute core logic with dependency
            var intermediateResult = await _{dependency1}.{DependencyMethod}Async(
                preparedData,
                cancellationToken);

            _logger.LogDebug(
                "Intermediate result obtained for request {RequestId}",
                request.Id);

            // Step 3: Optional processing with optional dependency
            if (_{dependency2} != null)
            {
                await _{dependency2}.ProcessAsync(intermediateResult, cancellationToken);
                _logger.LogDebug("Optional processing completed for request {RequestId}", request.Id);
            }

            // Step 4: Build result
            var result = BuildResult(intermediateResult, request);

            _logger.LogInformation(
                "{MethodName} completed successfully for request {RequestId}",
                nameof({MethodName}Async),
                request.Id);

            return result;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning(
                "{MethodName} was cancelled for request {RequestId}",
                nameof({MethodName}Async),
                request.Id);
            throw; // Re-throw cancellation exceptions
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(
                ex,
                "Invalid operation in {MethodName} for request {RequestId}: {Message}",
                nameof({MethodName}Async),
                request.Id,
                ex.Message);
            throw; // Re-throw known exceptions
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unexpected error in {MethodName} for request {RequestId}: {Message}",
                nameof({MethodName}Async),
                request.Id,
                ex.Message);
            throw; // Re-throw unexpected exceptions
        }
    }

    /// <summary>
    /// Validates the request object.
    /// </summary>
    /// <param name="request">The request to validate</param>
    /// <exception cref="ArgumentException">Thrown when validation fails</exception>
    private void ValidateRequest({RequestType} request)
    {
        if (request.Id == Guid.Empty)
        {
            throw new ArgumentException("Request ID cannot be empty", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ArgumentException("Request name cannot be empty", nameof(request));
        }

        if (request.Value < 0)
        {
            throw new ArgumentException("Request value must be non-negative", nameof(request));
        }

        // Add more validation as needed
        _logger.LogDebug("Request {RequestId} validation passed", request.Id);
    }

    /// <summary>
    /// Prepares data from the request for processing.
    /// </summary>
    /// <param name="request">The source request</param>
    /// <returns>Prepared data</returns>
    private {PreparedDataType} PrepareData({RequestType} request)
    {
        var prepared = new {PreparedDataType}
        {
            Id = request.Id,
            ProcessedName = request.Name.Trim().ToUpperInvariant(),
            Timestamp = DateTime.UtcNow,
            // Map other properties
        };

        _logger.LogDebug(
            "Prepared data for request {RequestId}",
            request.Id);

        return prepared;
    }

    /// <summary>
    /// Builds the final result from intermediate data.
    /// </summary>
    /// <param name="intermediateResult">The intermediate result from processing</param>
    /// <param name="originalRequest">The original request</param>
    /// <returns>Final result</returns>
    private {ResultType} BuildResult(
        {IntermediateResultType} intermediateResult,
        {RequestType} originalRequest)
    {
        var result = new {ResultType}
        {
            Id = originalRequest.Id,
            Success = true,
            Data = intermediateResult.ProcessedData,
            CompletedAt = DateTime.UtcNow,
            // Map other properties
        };

        _logger.LogDebug(
            "Result built for request {RequestId}: Success={Success}",
            originalRequest.Id,
            result.Success);

        return result;
    }

    #region Optional Helper Methods

    /// <summary>
    /// Example helper method for complex calculations.
    /// </summary>
    /// <param name="input">Input value</param>
    /// <returns>Calculated result</returns>
    private int CalculateSomething(int input)
    {
        // Complex calculation logic
        var result = input * 2 + 10;

        _logger.LogTrace("Calculated {Result} from input {Input}", result, input);

        return result;
    }

    /// <summary>
    /// Example helper method for status checks.
    /// </summary>
    /// <param name="status">Status to check</param>
    /// <returns>True if status is valid for processing</returns>
    private bool IsValidStatus({StatusEnum} status)
    {
        return status == {StatusEnum}.Active || status == {StatusEnum}.Pending;
    }

    #endregion
}

/*
 * USAGE INSTRUCTIONS:
 *
 * 1. Copy this template to your service file location
 * 2. Replace placeholders:
 *    - {ProjectName} → Domain, Infrastructure, Orchestrator, or Api
 *    - {ServiceName} → Name of your service (e.g., UserAuthenticationService)
 *    - {ServiceDescription} → Brief description of what the service does
 *    - {Responsibility1/2/3} → List the main responsibilities
 *    - {Dependency1/2} → Names of injected dependencies
 *    - {ConfigurationClass} → Configuration class (e.g., JwtConfiguration)
 *    - {MethodName} → Name of the main method (e.g., AuthenticateAsync)
 *    - {MethodDescription} → What the method does
 *    - {RequestType} → Input request model
 *    - {ResultType} → Output result model
 *    - {ReturnDescription} → Description of what's returned
 *    - {PreparedDataType} → Type for prepared/transformed data
 *    - {IntermediateResultType} → Type for intermediate processing results
 *    - {StatusEnum} → Enum type for status checks
 *
 * 3. Create the interface:
 *    - Create I{ServiceName}.cs in Interfaces/ folder
 *    - Define public method signatures
 *
 * 4. Register service in Program.cs:
 *    builder.Services.AddScoped<I{ServiceName}, {ServiceName}>();
 *    // Or AddSingleton/AddTransient depending on lifetime needs
 *
 * 5. Write tests BEFORE implementing (TDD):
 *    - Use test-template.cs to create {ServiceName}Tests.cs
 *    - Follow Red-Green-Refactor cycle
 *
 * 6. Follow best practices:
 *    - ✅ Validate all inputs
 *    - ✅ Log important operations (Info) and errors (Error)
 *    - ✅ Use structured logging with parameters
 *    - ✅ Handle cancellation properly
 *    - ✅ Throw specific exceptions with clear messages
 *    - ✅ Document all public methods with XML comments
 *    - ✅ Keep methods focused (Single Responsibility Principle)
 *    - ✅ Use async/await for I/O operations
 *    - ✅ Pass CancellationToken to all async methods
 *
 * EXAMPLE USAGE:
 *
 * Example 1: User Authentication Service
 * - {ServiceName} → UserAuthenticationService
 * - {MethodName} → AuthenticateAsync
 * - {Dependency1} → UserRepository
 * - {Dependency2} → EmailService (optional)
 * - {RequestType} → AuthenticationRequest (username, password)
 * - {ResultType} → AuthenticationResult (token, expiration)
 *
 * Example 2: Deployment Orchestration Service
 * - {ServiceName} → DeploymentOrchestrator
 * - {MethodName} → DeployAsync
 * - {Dependency1} → DeploymentStrategy
 * - {Dependency2} → NotificationService (optional)
 * - {RequestType} → DeploymentRequest
 * - {ResultType} → DeploymentResult
 *
 * TESTING:
 *
 * After creating the service:
 * 1. Create tests using test-template.cs
 * 2. Test all paths:
 *    - Happy path (valid input, successful execution)
 *    - Null/invalid input (ArgumentNullException, ArgumentException)
 *    - Error cases (InvalidOperationException)
 *    - Cancellation (OperationCanceledException)
 * 3. Verify logging:
 *    - Info logs for successful operations
 *    - Error logs for exceptions
 * 4. Verify dependency calls:
 *    - Mock.Verify() that dependencies called correctly
 *
 * SERVICE LIFETIME:
 *
 * Choose appropriate lifetime based on state:
 * - Scoped: Service with request-scoped state (most common for ASP.NET Core)
 * - Singleton: Stateless service, shared across application
 * - Transient: New instance every time (use for lightweight, stateless services)
 *
 * Example registration:
 * ```csharp
 * // In Program.cs or Startup.cs
 * builder.Services.AddScoped<IUserAuthenticationService, UserAuthenticationService>();
 * builder.Services.AddSingleton<IConfigurationService, ConfigurationService>();
 * builder.Services.AddTransient<IEmailService, EmailService>();
 * ```
 */
