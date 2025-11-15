using HotSwap.Distributed.Api.Middleware;
using HotSwap.Distributed.Api.Services;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Infrastructure.Coordination;
using HotSwap.Distributed.Infrastructure.Interfaces;
using HotSwap.Distributed.Infrastructure.Metrics;
using HotSwap.Distributed.Infrastructure.Notifications;
using HotSwap.Distributed.Infrastructure.Security;
using HotSwap.Distributed.Infrastructure.Telemetry;
using HotSwap.Distributed.Orchestrator.Core;
using HotSwap.Distributed.Orchestrator.Interfaces;
using HotSwap.Distributed.Orchestrator.Services;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Distributed Kernel Orchestration API",
        Version = "v1",
        Description = "API for managing distributed kernel module deployments",
        Contact = new Microsoft.OpenApi.Models.OpenApiContact
        {
            Name = "Distributed Kernel Team",
            Email = "team@example.com"
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
            .AddHttpClientInstrumentation()
            .AddConsoleExporter();

        // Add Jaeger exporter if configured
        var jaegerEndpoint = builder.Configuration["Telemetry:JaegerEndpoint"];
        if (!string.IsNullOrEmpty(jaegerEndpoint))
        {
            // Jaeger configuration would go here
        }
    });

// Register infrastructure services
builder.Services.AddSingleton<TelemetryProvider>();
builder.Services.AddSingleton<IMetricsProvider, InMemoryMetricsProvider>();
builder.Services.AddSingleton<IModuleVerifier>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<ModuleVerifier>>();
    var strictMode = builder.Configuration.GetValue<bool>("Security:StrictMode", true);
    return new ModuleVerifier(logger, strictMode);
});

// Register Redis if configured
var redisConnection = builder.Configuration["Redis:ConnectionString"];
if (!string.IsNullOrEmpty(redisConnection))
{
    builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
        ConnectionMultiplexer.Connect(redisConnection));

    builder.Services.AddSingleton<IDistributedLock, RedisDistributedLock>();
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
builder.Services.AddSingleton<IApprovalService, ApprovalService>();
builder.Services.AddHostedService<ApprovalTimeoutBackgroundService>();

// Register orchestrator as singleton
builder.Services.AddSingleton<DistributedKernelOrchestrator>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<DistributedKernelOrchestrator>>();
    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
    var metricsProvider = sp.GetRequiredService<IMetricsProvider>();
    var moduleVerifier = sp.GetRequiredService<IModuleVerifier>();
    var telemetry = sp.GetRequiredService<TelemetryProvider>();
    var pipelineConfig = sp.GetRequiredService<PipelineConfiguration>();

    var orchestrator = new DistributedKernelOrchestrator(
        logger,
        loggerFactory,
        metricsProvider,
        moduleVerifier,
        telemetry,
        pipelineConfig);

    // Initialize clusters on startup
    orchestrator.InitializeClustersAsync().GetAwaiter().GetResult();

    return orchestrator;
});

// Add health checks
builder.Services.AddHealthChecks();

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

// 5. HTTPS redirection
app.UseHttpsRedirection();

// 6. CORS
app.UseCors();

// 7. Rate limiting (after CORS, before authorization)
app.UseMiddleware<RateLimitingMiddleware>();

// 8. Authentication & Authorization (would be added here)
app.UseAuthorization();

// 9. Map controllers
app.MapControllers();

// 10. Health check endpoint
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
