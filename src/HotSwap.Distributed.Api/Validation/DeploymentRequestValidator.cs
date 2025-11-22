using System.Text.RegularExpressions;
using HotSwap.Distributed.Api.Models;
using HotSwap.Distributed.Domain.Enums;

namespace HotSwap.Distributed.Api.Validation;

/// <summary>
/// Validator for deployment request models
/// </summary>
public class DeploymentRequestValidator
{
    private static readonly Regex _emailRegex = new(
        @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$",
        RegexOptions.Compiled);

    private static readonly Regex _versionRegex = new(
        @"^\d+\.\d+\.\d+(-[a-zA-Z0-9]+)?$",
        RegexOptions.Compiled);

    private static readonly Regex _moduleNameRegex = new(
        @"^[a-z0-9][a-z0-9-]{1,62}[a-z0-9]$",
        RegexOptions.Compiled);

    /// <summary>
    /// Validates a deployment request
    /// </summary>
    /// <param name="request">The deployment request to validate</param>
    /// <param name="errors">Collection of validation errors</param>
    /// <returns>True if valid, false otherwise</returns>
    public static bool Validate(CreateDeploymentRequest request, out List<string> errors)
    {
        errors = new List<string>();

        if (request == null)
        {
            errors.Add("Request body is required");
            return false;
        }

        // Validate module name
        if (string.IsNullOrWhiteSpace(request.ModuleName))
        {
            errors.Add("ModuleName is required");
        }
        else if (request.ModuleName.Length < 3 || request.ModuleName.Length > 64)
        {
            errors.Add("ModuleName must be between 3 and 64 characters");
        }
        else if (!_moduleNameRegex.IsMatch(request.ModuleName))
        {
            errors.Add("ModuleName must contain only lowercase letters, numbers, and hyphens, and must start and end with an alphanumeric character");
        }

        // Validate version
        if (string.IsNullOrWhiteSpace(request.Version))
        {
            errors.Add("Version is required");
        }
        else if (!_versionRegex.IsMatch(request.Version))
        {
            errors.Add("Version must be in semantic versioning format (e.g., 1.0.0 or 1.0.0-beta)");
        }

        // Validate target environment
        if (string.IsNullOrWhiteSpace(request.TargetEnvironment))
        {
            errors.Add("TargetEnvironment is required");
        }
        else if (!Enum.TryParse<EnvironmentType>(request.TargetEnvironment, true, out _))
        {
            errors.Add($"TargetEnvironment must be one of: {string.Join(", ", Enum.GetNames<EnvironmentType>())}");
        }

        // Validate requester email
        if (string.IsNullOrWhiteSpace(request.RequesterEmail))
        {
            errors.Add("RequesterEmail is required");
        }
        else if (!_emailRegex.IsMatch(request.RequesterEmail))
        {
            errors.Add("RequesterEmail must be a valid email address");
        }

        // Validate description (optional but if provided, must be reasonable length)
        if (!string.IsNullOrWhiteSpace(request.Description) && request.Description.Length > 1000)
        {
            errors.Add("Description must not exceed 1000 characters");
        }

        // Validate metadata (optional but if provided, must have reasonable limits)
        if (request.Metadata != null)
        {
            if (request.Metadata.Count > 50)
            {
                errors.Add("Metadata cannot contain more than 50 entries");
            }

            foreach (var kvp in request.Metadata)
            {
                if (string.IsNullOrWhiteSpace(kvp.Key))
                {
                    errors.Add("Metadata keys cannot be empty");
                }
                else if (kvp.Key.Length > 100)
                {
                    errors.Add($"Metadata key '{kvp.Key}' exceeds 100 characters");
                }

                if (kvp.Value != null && kvp.Value.Length > 500)
                {
                    errors.Add($"Metadata value for key '{kvp.Key}' exceeds 500 characters");
                }
            }
        }

        return errors.Count == 0;
    }

    /// <summary>
    /// Validates a deployment request and throws an exception if invalid
    /// </summary>
    /// <param name="request">The deployment request to validate</param>
    /// <exception cref="ValidationException">Thrown when validation fails</exception>
    public static void ValidateAndThrow(CreateDeploymentRequest request)
    {
        if (!Validate(request, out var errors))
        {
            throw new ValidationException(errors);
        }
    }
}

/// <summary>
/// Exception thrown when validation fails
/// </summary>
public class ValidationException : Exception
{
    public List<string> Errors { get; }

    public ValidationException(List<string> errors)
        : base("Validation failed: " + string.Join("; ", errors))
    {
        Errors = errors;
    }
}
