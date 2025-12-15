using FinDocAnalyzer.Extensions;

var builder = WebApplication.CreateBuilder(args);

// CONFIGURE FIXED PORTS
builder.WebHost.UseUrls("http://localhost:5070", "https://localhost:7070");

// SERVICE CONFIGURATION
builder.Services.AddControllers();

// Swagger/OpenAPI with file upload support
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "FinDoc Analyzer API",
        Version = "v1.0",
        Description = "API for analyzing financial PDF reports using AI (GPT-4o)\n\n" +
                     "✅ Universal support for any report type (Brazilian, Offshore, International)\n" +
                     "✅ Automatic PDF caching (SHA256)\n" +
                     "✅ User tracking and audit logs (GDPR compliant)\n" +
                     "✅ AI cost and token metadata"
    });

    options.OperationFilter<FileUploadOperationFilter>();
});

// CONFIGURE FinDocAnalyser SDK with Microsoft.Extensions.AI
var azureApiKey = builder.Configuration["AzureOpenAI:ApiKey"];
var azureEndpoint = builder.Configuration["AzureOpenAI:Endpoint"];

if (string.IsNullOrEmpty(azureApiKey) || string.IsNullOrEmpty(azureEndpoint))
{
    throw new InvalidOperationException(
        "Azure OpenAI not configured. " +
        "Add 'AzureOpenAI:ApiKey' and 'AzureOpenAI:Endpoint' to appsettings.json or use User Secrets.");
}

builder.Services.AddFinDocAnalyser(options =>
{
    // Azure OpenAI
    options.AzureOpenAI.ApiKey = azureApiKey;
    options.AzureOpenAI.Endpoint = azureEndpoint;
    options.AzureOpenAI.DeploymentName = builder.Configuration["AzureOpenAI:DeploymentName"] ?? "gpt-4o";

    // Enable PDF caching (prevents reprocessing)
    options.EnablePdfCache = true;

    // Storage (InMemory for development)
    options.StorageType = StorageType.InMemory;

    // Telemetry (disabled by default)
    options.EnableTelemetry = false;
});

// CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// LOGGING
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Information);

var app = builder.Build();

// HTTP PIPELINE CONFIGURATION
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "FinDoc Analyzer API v1");
        options.RoutePrefix = string.Empty; // Swagger at root
        options.DocumentTitle = "FinDoc Analyzer - Financial Report Analysis API";
    });
}

app.UseCors();
app.UseAuthorization();
app.MapControllers();

app.Logger.LogInformation("====================================");
app.Logger.LogInformation("FinDoc Analyzer API iniciada!");
app.Logger.LogInformation("Framework: .NET 10");
app.Logger.LogInformation("AI: Azure OpenAI (Microsoft.Extensions.AI)");
app.Logger.LogInformation("====================================");
app.Logger.LogInformation("📄 Swagger UI: http://localhost:5070/");
app.Logger.LogInformation("📋 OpenAPI Spec: http://localhost:5070/swagger/v1/swagger.json");
app.Logger.LogInformation("====================================");

app.Run();

// FILTER FOR FILE UPLOAD IN SWAGGER
public class FileUploadOperationFilter : Swashbuckle.AspNetCore.SwaggerGen.IOperationFilter
{
    public void Apply(Microsoft.OpenApi.Models.OpenApiOperation operation, Swashbuckle.AspNetCore.SwaggerGen.OperationFilterContext context)
    {
        var formFileParams = context.MethodInfo.GetParameters()
            .Where(p => p.ParameterType == typeof(IFormFile))
            .ToList();

        if (!formFileParams.Any())
            return;

        operation.Parameters?.Clear();

        operation.RequestBody = new Microsoft.OpenApi.Models.OpenApiRequestBody
        {
            Content = new Dictionary<string, Microsoft.OpenApi.Models.OpenApiMediaType>
            {
                ["multipart/form-data"] = new Microsoft.OpenApi.Models.OpenApiMediaType
                {
                    Schema = new Microsoft.OpenApi.Models.OpenApiSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, Microsoft.OpenApi.Models.OpenApiSchema>
                        {
                            ["file"] = new Microsoft.OpenApi.Models.OpenApiSchema
                            {
                                Type = "string",
                                Format = "binary",
                                Description = "Financial report PDF file (max 50MB)"
                            },
                            ["userId"] = new Microsoft.OpenApi.Models.OpenApiSchema
                            {
                                Type = "string",
                                Description = "User ID (optional - for multi-tenant tracking)"
                            },
                            ["clientId"] = new Microsoft.OpenApi.Models.OpenApiSchema
                            {
                                Type = "string",
                                Description = "Client ID (optional - for multi-tenant tracking)"
                            }
                        },
                        Required = new HashSet<string> { "file" }
                    }
                }
            }
        };
    }
}
