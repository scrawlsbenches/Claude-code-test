using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Infrastructure.Coordination;
using HotSwap.Distributed.Infrastructure.Interfaces;
using HotSwap.Distributed.Infrastructure.Metrics;
using HotSwap.Distributed.Infrastructure.Security;
using HotSwap.Distributed.Infrastructure.Telemetry;
using HotSwap.Distributed.Orchestrator.Core;
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

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Distributed Kernel API v1");
        options.RoutePrefix = string.Empty; // Serve Swagger UI at root
    });
}

app.UseSerilogRequestLogging();

app.UseHttpsRedirection();

app.UseCors();

app.UseAuthorization();

app.MapControllers();

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
