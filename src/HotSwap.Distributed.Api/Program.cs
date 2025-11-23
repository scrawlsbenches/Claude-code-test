using System.Text;
using HotSwap.Distributed.Api.Hubs;
using HotSwap.Distributed.Api.Middleware;
using HotSwap.Distributed.Api.Services;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Infrastructure.Authentication;
using HotSwap.Distributed.Infrastructure.Coordination;
using HotSwap.Distributed.Infrastructure.Deployments;
using HotSwap.Distributed.Infrastructure.Interfaces;
using HotSwap.Distributed.Infrastructure.Metrics;
using HotSwap.Distributed.Infrastructure.Notifications;
using HotSwap.Distributed.Infrastructure.Security;
using HotSwap.Distributed.Infrastructure.Telemetry;
using HotSwap.Distributed.Infrastructure.Tenants;
using HotSwap.Distributed.Infrastructure.Data;
using HotSwap.Distributed.Infrastructure.Repositories;
using HotSwap.Distributed.Infrastructure.Services;
using HotSwap.Distributed.Orchestrator.Core;
using HotSwap.Distributed.Orchestrator.Interfaces;
using HotSwap.Distributed.Orchestrator.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configure request size limits (Security: DoS protection)
builder.Services.Configure<KestrelServerOptions>(options =>
{
    options.Limits.MaxRequestBodySize = 10 * 1024 * 1024; // 10 MB
});

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 10 * 1024 * 1024; // 10 MB
});

// Configure Serilog
// Note: Console sink is configured in appsettings.json - don't add it again here
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .CreateLogger();

builder.Host.UseSerilog();

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Add memory cache for deployment tracking
builder.Services.AddMemoryCache();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Distributed Kernel Orchestration API",
        Version = "v1",
        Description = "API for managing distributed kernel module deployments with JWT authentication",
        Contact = new OpenApiContact
        {
            Name = "Distributed Kernel Team",
            Email = "team@example.com"
        }
    });

    // Add JWT Bearer authentication to Swagger
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token in the text input below.\r\n\r\nExample: \"Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        BearerFormat = "JWT"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });

    // Include XML comments
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});

// Configure OpenTelemetry
builder.Services.AddOpenTelemetry()
    .WithTracing(tracerProviderBuilder =>
    {
        tracerProviderBuilder
            .AddSource(TelemetryProvider.ServiceName)
            .SetResourceBuilder(ResourceBuilder.CreateDefault()
                .AddService(TelemetryProvider.ServiceName, serviceVersion: TelemetryProvider.ServiceVersion))
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation();

        // Only add console exporter if explicitly enabled (disabled for tests to reduce log spam)
        var enableConsoleExporter = builder.Configuration.GetValue<bool>("Telemetry:EnableConsoleExporter", false);
        if (enableConsoleExporter)
        {
            tracerProviderBuilder.AddConsoleExporter();
        }

        // Add Jaeger exporter if configured
        var jaegerEndpoint = builder.Configuration["Telemetry:JaegerEndpoint"];
        if (!string.IsNullOrEmpty(jaegerEndpoint))
        {
            // Jaeger configuration would go here
        }
    })
    .WithMetrics(metricsProviderBuilder =>
    {
        metricsProviderBuilder
            .SetResourceBuilder(ResourceBuilder.CreateDefault()
                .AddService(TelemetryProvider.ServiceName, serviceVersion: TelemetryProvider.ServiceVersion))
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation()
            .AddProcessInstrumentation()
            // Add custom deployment metrics
            .AddMeter(DeploymentMetrics.MeterName)
            // Add Prometheus exporter for /metrics endpoint
            .AddPrometheusExporter();
    });

// Register JWT configuration
// SECURITY: Require explicit JWT configuration - no defaults in production
var jwtSecretKey = builder.Configuration["Jwt:SecretKey"];
if (string.IsNullOrWhiteSpace(jwtSecretKey))
{
    if (builder.Environment.IsProduction())
    {
        throw new InvalidOperationException(
            "JWT SecretKey is required in production. Set via environment variable JWT__SECRETKEY or appsettings.json");
    }

    // Development/Test fallback with warning
    jwtSecretKey = "DistributedKernelSecretKey-ChangeInProduction-MinimumLength32Characters";
    Log.Warning("Using default JWT secret key - THIS IS NOT SECURE FOR PRODUCTION");
}

var jwtConfig = new JwtConfiguration
{
    SecretKey = jwtSecretKey,
    Issuer = builder.Configuration["Jwt:Issuer"] ?? "DistributedKernelOrchestrator",
    Audience = builder.Configuration["Jwt:Issuer"] ?? "DistributedKernelApi",
    ExpirationMinutes = builder.Configuration.GetValue<int>("Jwt:ExpirationMinutes", 60)
};
builder.Services.AddSingleton(jwtConfig);

// Register authentication services
builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();
builder.Services.AddSingleton<IUserRepository, InMemoryUserRepository>();

// Register messaging services
// Use PostgreSQL-backed message queue (or in-memory for development)
var usePostgresMessageQueue = builder.Configuration.GetValue<bool>("DistributedSystems:UsePostgresMessageQueue", true);
if (usePostgresMessageQueue)
{
    builder.Services.AddScoped<IMessageQueue, HotSwap.Distributed.Infrastructure.Messaging.PostgresMessageQueue>();
}
else
{
    builder.Services.AddSingleton<IMessageQueue, HotSwap.Distributed.Infrastructure.Messaging.InMemoryMessageQueue>();
}
builder.Services.AddSingleton<IMessagePersistence, HotSwap.Distributed.Infrastructure.Messaging.InMemoryMessagePersistence>();

// Configure JWT authentication
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = !builder.Environment.IsDevelopment(); // Require HTTPS in production
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtConfig.SecretKey)),
        ValidateIssuer = true,
        ValidIssuer = jwtConfig.Issuer,
        ValidateAudience = true,
        ValidAudience = jwtConfig.Audience,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero // No tolerance for expired tokens
    };
});

builder.Services.AddAuthorization();

// Configure HSTS (HTTP Strict Transport Security)
builder.Services.AddHsts(options =>
{
    var hstsConfig = builder.Configuration.GetSection("Hsts");
    // MaxAge is configured in seconds (e.g., 31536000 = 1 year)
    options.MaxAge = TimeSpan.FromSeconds(hstsConfig.GetValue<int>("MaxAge", 31536000));
    options.IncludeSubDomains = hstsConfig.GetValue<bool>("IncludeSubDomains", true);
    options.Preload = hstsConfig.GetValue<bool>("Preload", false);
});

// Register infrastructure services
builder.Services.AddSingleton<TelemetryProvider>();
builder.Services.AddSingleton<IMetricsProvider, InMemoryMetricsProvider>();
builder.Services.AddSingleton<DeploymentMetrics>();
builder.Services.AddSingleton<IModuleVerifier>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<ModuleVerifier>>();
    var strictMode = builder.Configuration.GetValue<bool>("Security:StrictMode", true);
    return new ModuleVerifier(logger, strictMode);
});

// Register distributed lock
// Use PostgreSQL advisory locks for production (true distributed locking)
// Fall back to in-memory for development/testing
var usePostgresLocks = builder.Configuration.GetValue<bool>("DistributedSystems:UsePostgresLocks", true);
if (usePostgresLocks)
{
    builder.Services.AddScoped<IDistributedLock, PostgresDistributedLock>();
    builder.Services.AddLogging(logging => logging.AddConsole());
}
else
{
    builder.Services.AddSingleton<IDistributedLock, InMemoryDistributedLock>();
}

// Register pipeline configuration
builder.Services.AddSingleton(sp =>
{
    var config = new PipelineConfiguration();
    builder.Configuration.GetSection("Pipeline").Bind(config);
    return config;
});

// Register approval workflow services
builder.Services.AddSingleton<INotificationService, LoggingNotificationService>();

// Use PostgreSQL-backed approval service (or in-memory for development)
var usePostgresApprovals = builder.Configuration.GetValue<bool>("DistributedSystems:UsePostgresApprovals", true);
if (usePostgresApprovals)
{
    builder.Services.AddScoped<IApprovalRepository, ApprovalRepository>();
    builder.Services.AddScoped<IApprovalService, ApprovalServiceRefactored>();
}
else
{
    builder.Services.AddSingleton<IApprovalService, ApprovalService>();
}

// Skip background services in Test environment to prevent test hangs
if (builder.Environment.EnvironmentName != "Test")
{
    builder.Services.AddHostedService<ApprovalTimeoutBackgroundService>();
}

// Register audit log service only if PostgreSQL is configured
if (!string.IsNullOrEmpty(builder.Configuration["ConnectionStrings:PostgreSql"]))
{
    builder.Services.AddSingleton<IAuditLogService, HotSwap.Distributed.Infrastructure.Services.AuditLogService>();

    // Skip background services in Test environment to prevent test hangs
    if (builder.Environment.EnvironmentName != "Test")
    {
        builder.Services.AddHostedService<AuditLogRetentionBackgroundService>();

        // Register deployment job processor (transactional outbox pattern)
        var usePostgresJobQueue = builder.Configuration.GetValue<bool>("DistributedSystems:UsePostgresJobQueue", true);
        if (usePostgresJobQueue)
        {
            builder.Services.AddHostedService<DeploymentJobProcessor>();
        }

        // Register message consumer service (PostgreSQL LISTEN/NOTIFY)
        if (usePostgresMessageQueue)
        {
            builder.Services.AddHostedService<MessageConsumerService>();
        }
    }
}
else
{
    // No PostgreSQL available, audit logging will be disabled
    Log.Warning("PostgreSQL not configured - audit logging disabled");
}

// Register deployment tracking service
builder.Services.AddSingleton<IDeploymentTracker, InMemoryDeploymentTracker>();

// Register deployment notification service
builder.Services.AddSingleton<IDeploymentNotifier, SignalRDeploymentNotifier>();

// Register tenant services
builder.Services.AddSingleton<ITenantRepository, InMemoryTenantRepository>();
builder.Services.AddSingleton<ITenantProvisioningService, TenantProvisioningService>();

// Register rate limit cleanup service
// Skip background services in Test environment to prevent test hangs
if (builder.Environment.EnvironmentName != "Test")
{
    builder.Services.AddHostedService<RateLimitCleanupService>();
}

// Register secret management services
builder.Services.AddSingleton<ISecretService>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<HotSwap.Distributed.Infrastructure.SecretManagement.InMemorySecretService>>();
    var secretService = new HotSwap.Distributed.Infrastructure.SecretManagement.InMemorySecretService(logger);

    // Seed JWT signing key so JwtTokenService can load it from secret service
    // This prevents the "JWT signing key not found" warning during startup
    var jwtConfig = sp.GetRequiredService<JwtConfiguration>();
    secretService.SeedSecretsAsync(new Dictionary<string, string>
    {
        { "jwt-signing-key", jwtConfig.SecretKey }
    }).GetAwaiter().GetResult();

    return secretService;
});

// Skip background services in Test environment to prevent test hangs
if (builder.Environment.EnvironmentName != "Test")
{
    builder.Services.AddHostedService<SecretRotationBackgroundService>();
}

// Note: Orchestrator initialization now happens synchronously after app.Build()
// to ensure it's ready before accepting requests (removed background service)

// Register orchestrator as singleton
builder.Services.AddSingleton<DistributedKernelOrchestrator>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<DistributedKernelOrchestrator>>();
    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
    var metricsProvider = sp.GetRequiredService<IMetricsProvider>();
    var moduleVerifier = sp.GetRequiredService<IModuleVerifier>();
    var telemetry = sp.GetRequiredService<TelemetryProvider>();
    var pipelineConfig = sp.GetRequiredService<PipelineConfiguration>();
    var deploymentTracker = sp.GetRequiredService<IDeploymentTracker>();
    var approvalService = sp.GetRequiredService<IApprovalService>();
    var auditLogService = sp.GetService<IAuditLogService>(); // Optional - may be null

    var orchestrator = new DistributedKernelOrchestrator(
        logger,
        loggerFactory,
        metricsProvider,
        moduleVerifier,
        telemetry,
        pipelineConfig,
        deploymentTracker,
        approvalService,
        auditLogService);

    // Note: Orchestrator is created here but clusters are initialized synchronously
    // after app.Build() to ensure it's ready before accepting requests

    return orchestrator;
});

// Add health checks
builder.Services.AddHealthChecks();

// Configure SignalR for real-time deployment updates
builder.Services.AddSignalR(options =>
{
    // Configure SignalR options
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
    options.MaximumReceiveMessageSize = 32 * 1024; // 32 KB per message
});

// Register middleware configurations
builder.Services.AddSingleton(sp =>
{
    var config = new RateLimitConfiguration();
    builder.Configuration.GetSection("RateLimiting").Bind(config);
    return config;
});

builder.Services.AddSingleton(sp =>
{
    var config = new SecurityHeadersConfiguration();
    builder.Configuration.GetSection("SecurityHeaders").Bind(config);
    return config;
});

// Add CORS with improved configuration
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
            ?? new[] { "http://localhost:3000", "http://localhost:8080" };

        if (builder.Environment.IsDevelopment())
        {
            // More permissive in development
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        }
        else
        {
            // Restrictive in production
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .AllowCredentials()
                  .SetIsOriginAllowedToAllowWildcardSubdomains();
        }
    });
});

var app = builder.Build();

// Initialize orchestrator clusters BEFORE starting the API
// This ensures the orchestrator is ready before any requests are handled
// Without this, first requests fail with "Orchestrator not initialized"
var orchestrator = app.Services.GetRequiredService<DistributedKernelOrchestrator>();
await orchestrator.InitializeClustersAsync();
Log.Information("Orchestrator clusters initialized successfully");

// Configure the HTTP request pipeline

// 1. Exception handling (must be first to catch all exceptions)
app.UseMiddleware<ExceptionHandlingMiddleware>();

// 2. Security headers (early in pipeline)
app.UseMiddleware<SecurityHeadersMiddleware>();

// 3. Serilog request logging
app.UseSerilogRequestLogging();

// 4. Swagger (only in development/staging)
if (!app.Environment.IsProduction())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Distributed Kernel API v1");
        options.RoutePrefix = string.Empty; // Serve Swagger UI at root
        options.DisplayRequestDuration();
        options.EnableTryItOutByDefault();
    });
}

// 5. Prometheus metrics endpoint (before rate limiting)
app.MapPrometheusScrapingEndpoint();
Log.Information("Prometheus metrics endpoint enabled at /metrics");

// 6. HSTS (HTTP Strict Transport Security) - only in production
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

// 7. HTTPS redirection - only in production (allows HTTP for testing/CI/CD)
if (app.Environment.IsProduction())
{
    app.UseHttpsRedirection();
}

// 8. CORS
app.UseCors();

// 9. Rate limiting (after CORS, before authentication)
app.UseMiddleware<RateLimitingMiddleware>();

// 9. Authentication & Authorization
app.UseAuthentication();
app.UseAuthorization();

// 10. Map controllers
app.MapControllers();

// 11. Map SignalR hub for real-time deployment updates
app.MapHub<DeploymentHub>("/hubs/deployment");

// 12. Health check endpoint
app.MapHealthChecks("/health");

// Graceful shutdown
var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
lifetime.ApplicationStopping.Register(async () =>
{
    Log.Information("Application shutting down...");

    var orchestrator = app.Services.GetRequiredService<DistributedKernelOrchestrator>();
    await orchestrator.DisposeAsync();

    Log.Information("Application shutdown complete");
});

Log.Information("Starting Distributed Kernel Orchestration API");
Log.Information("Swagger UI available at: http://localhost:5000");

app.Run();

// Make Program class accessible for integration testing
public partial class Program { }
