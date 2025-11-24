using FinDocAnalyzer.Extensions;

var builder = WebApplication.CreateBuilder(args);

// CONFIGURAR PORTAS FIXAS
builder.WebHost.UseUrls("http://localhost:5070", "https://localhost:7070");

// CONFIGURAÇÃO DE SERVIÇOS
builder.Services.AddControllers();

// Swagger/OpenAPI com suporte para upload de arquivos
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "FinDoc Analyzer API",
        Version = "v1.0 - .NET 10 + Microsoft.Extensions.AI",
        Description = "API para análise de relatórios financeiros em PDF usando IA (GPT-4o)\n\n" +
                     "✅ Suporte universal para qualquer tipo de relatório (Brasileiro, Offshore, Internacional)\n" +
                     "✅ Cache automático de PDFs (SHA256)\n" +
                     "✅ Tracking de usuários e audit logs (LGPD compliant)\n" +
                     "✅ Metadados de custos e tokens da IA"
    });

    options.OperationFilter<FileUploadOperationFilter>();
});

// CONFIGURA FinDocAnalyser SDK com Microsoft.Extensions.AI
var openAiApiKey = builder.Configuration["OpenAI:ApiKey"];

if (string.IsNullOrEmpty(openAiApiKey))
{
    throw new InvalidOperationException(
        "Chave da API OpenAI não configurada. " +
        "Adicione 'OpenAI:ApiKey' no appsettings.json ou use User Secrets.");
}

builder.Services.AddFinDocAnalyser(options =>
{
    // Provider de IA (OpenAI, Azure OpenAI, ou Ollama)
    options.AiProvider = AiProvider.OpenAI;
    options.OpenAI.ApiKey = openAiApiKey;
    options.OpenAI.Model = builder.Configuration["OpenAI:Model"] ?? "gpt-4o";

    // Habilita cache de PDFs (evita reprocessamento)
    options.EnablePdfCache = true;

    // Storage (InMemory para desenvolvimento)
    options.StorageType = StorageType.InMemory;

    // Telemetria (desabilitado por padrão)
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

// CONFIGURAÇÃO DO PIPELINE HTTP
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "FinDoc Analyzer API v1");
        options.RoutePrefix = string.Empty;
        options.DocumentTitle = "FinDoc Analyzer - API de Análise de Relatórios Financeiros";
    });
}

app.UseCors();
app.UseAuthorization();
app.MapControllers();

app.Logger.LogInformation("====================================");
app.Logger.LogInformation("FinDoc Analyzer API iniciada!");
app.Logger.LogInformation("Framework: .NET 10");
app.Logger.LogInformation("AI: Microsoft.Extensions.AI + GPT-4o");
app.Logger.LogInformation("Swagger UI: http://localhost:5070/");
app.Logger.LogInformation("====================================");

app.Run();

// FILTRO PARA UPLOAD DE ARQUIVOS NO SWAGGER
public class FileUploadOperationFilter : Swashbuckle.AspNetCore.SwaggerGen.IOperationFilter
{
    public void Apply(Microsoft.OpenApi.Models.OpenApiOperation operation, Swashbuckle.AspNetCore.SwaggerGen.OperationFilterContext context)
    {
        // Detecta parâmetro List<IFormFile>
        var listFormFileParams = context.MethodInfo.GetParameters()
            .Where(p => p.ParameterType == typeof(List<IFormFile>))
            .ToList();

        if (listFormFileParams.Any())
        {
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
                                ["files"] = new Microsoft.OpenApi.Models.OpenApiSchema
                                {
                                    Type = "array",
                                    Items = new Microsoft.OpenApi.Models.OpenApiSchema
                                    {
                                        Type = "string",
                                        Format = "binary"
                                    },
                                    Description = "Arquivos PDF dos relatórios financeiros (1-20 arquivos, máx 50MB cada)"
                                }
                            },
                            Required = new HashSet<string> { "files" }
                        }
                    }
                }
            };
            return;
        }

        // Detecta parâmetro IFormFile (fallback)
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
                                Description = "Arquivo PDF do relatório financeiro (máx 50MB)"
                            }
                        },
                        Required = new HashSet<string> { "file" }
                    }
                }
            }
        };
    }
}
