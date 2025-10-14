using FinDocAnalyzer.Core.Interfaces;
using FinDocAnalyzer.Core.Services;
using FinDocAnalyzer.Infrastructure.AI;
using FinDocAnalyzer.Infrastructure.Pdf;
using FinDocAnalyzer.Infrastructure.Storage;
using OpenAI.Chat;

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
        Version = "v1",
        Description = "API para análise de relatórios financeiros em PDF"
    });

    options.OperationFilter<FileUploadOperationFilter>();
});

// CONFIGURAÇÃO DO OPENAI
var openAiApiKey = builder.Configuration["OpenAI:ApiKey"];
var openAiModel = builder.Configuration["OpenAI:Model"] ?? "gpt-4o";

if (string.IsNullOrEmpty(openAiApiKey))
{
    throw new InvalidOperationException(
        "Chave da API OpenAI não configurada. " +
        "Adicione 'OpenAI:ApiKey' no appsettings.json ou use User Secrets.");
}

builder.Services.AddSingleton<ChatClient>(serviceProvider =>
{
    var openAiClient = new OpenAI.OpenAIClient(openAiApiKey);
    return openAiClient.GetChatClient(openAiModel);
});

// REGISTRO DE SERVIÇOS (DI)
builder.Services.AddScoped<AnalysisOrchestrator>();
builder.Services.AddScoped<IPdfExtractor, PdfPigExtractor>();
builder.Services.AddScoped<IAiAnalyzer, OpenAiAnalyzer>();
builder.Services.AddSingleton<IResultStore, InMemoryResultStore>();

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
    });
}

app.UseCors();
app.UseAuthorization();
app.MapControllers();

app.Logger.LogInformation("==============================================");
app.Logger.LogInformation("FinDoc Analyzer API iniciada com sucesso!");
app.Logger.LogInformation("Swagger UI: http://localhost:5070/");
app.Logger.LogInformation("==============================================");

app.Run();

// FILTRO PARA UPLOAD DE ARQUIVOS NO SWAGGER
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
                                Description = "Arquivo PDF do relatório financeiro"
                            }
                        },
                        Required = new HashSet<string> { "file" }
                    }
                }
            }
        };
    }
}