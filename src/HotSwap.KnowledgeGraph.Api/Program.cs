using HotSwap.KnowledgeGraph.Api.Services;
using HotSwap.KnowledgeGraph.Infrastructure.Data;
using HotSwap.KnowledgeGraph.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Serilog;

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

// Configure Swagger
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Knowledge Graph API",
        Version = "v1",
        Description = "API for managing a knowledge graph with document ingestion capabilities",
        Contact = new OpenApiContact
        {
            Name = "Knowledge Graph Team",
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

// Configure database
var connectionString = builder.Configuration.GetConnectionString("PostgreSql");
if (!string.IsNullOrEmpty(connectionString))
{
    // Use PostgreSQL in production
    builder.Services.AddDbContext<GraphDbContext>(options =>
        options.UseNpgsql(connectionString));
    builder.Services.AddScoped<IGraphRepository, PostgresGraphRepository>();
    Log.Information("Using PostgreSQL database");
}
else
{
    // Use in-memory database for development/testing
    builder.Services.AddDbContext<GraphDbContext>(options =>
        options.UseInMemoryDatabase("KnowledgeGraph"));
    builder.Services.AddScoped<IGraphRepository, PostgresGraphRepository>();
    Log.Warning("Using in-memory database - data will not persist");
}

// Register document ingestion service
builder.Services.AddScoped<IDocumentIngestionService, DocumentIngestionService>();

// Add health checks
builder.Services.AddHealthChecks();

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        if (builder.Environment.IsDevelopment())
        {
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        }
        else
        {
            var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                ?? Array.Empty<string>();
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        }
    });
});

var app = builder.Build();

// Ensure database is created (for development with in-memory)
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<GraphDbContext>();
    dbContext.Database.EnsureCreated();
}

// Configure the HTTP request pipeline

// Serilog request logging
app.UseSerilogRequestLogging();

// Swagger (available in non-production environments)
if (!app.Environment.IsProduction())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Knowledge Graph API v1");
        options.RoutePrefix = string.Empty; // Serve Swagger UI at root
        options.DisplayRequestDuration();
        options.EnableTryItOutByDefault();
    });
}

// CORS
app.UseCors();

// Map controllers
app.MapControllers();

// Health check endpoint
app.MapHealthChecks("/health");

Log.Information("Starting Knowledge Graph API");
Log.Information("Swagger UI available at: http://localhost:5001");

app.Run();

// Make Program class accessible for integration testing
public partial class Program { }
