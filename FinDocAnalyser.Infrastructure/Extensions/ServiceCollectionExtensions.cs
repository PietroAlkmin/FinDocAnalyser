using FinDocAnalyzer.Core.Interfaces;
using FinDocAnalyzer.Core.Services;
using FinDocAnalyzer.Infrastructure.AI;
using FinDocAnalyzer.Infrastructure.Caching;
using FinDocAnalyzer.Infrastructure.Currency;
using FinDocAnalyzer.Infrastructure.Pdf;
using FinDocAnalyzer.Infrastructure.Storage;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FinDocAnalyzer.Extensions;

/// <summary>
/// Extension methods para configurar o FinDocAnalyser SDK
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adiciona serviços do FinDocAnalyser com configuração padrão
    /// </summary>
    public static IServiceCollection AddFinDocAnalyser(
        this IServiceCollection services,
        Action<FinDocAnalyserOptions> configure)
    {
        var options = new FinDocAnalyserOptions();
        configure(options);

        // Valida configuração
        options.Validate();

        // Registra serviços core
        services.AddScoped<AnalysisOrchestrator>();
        services.AddScoped<ConsolidationService>();
        services.AddScoped<IPdfExtractor, PdfPigExtractor>();
        services.AddScoped<IAiAnalyzer, AiDocumentAnalyzer>();

        // Conversor de moedas (Banco Central do Brasil - USD↔BRL apenas)
        services.AddHttpClient<ICurrencyConverter, BcbUsdBrlConverter>(client =>
        {
            client.BaseAddress = new Uri("https://olinda.bcb.gov.br");
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        // Cache de PDFs
        if (options.EnablePdfCache)
        {
            services.TryAddSingleton<IPdfCache, InMemoryPdfCache>();
        }

        // Storage (padrão: InMemory)
        if (options.StorageType == StorageType.InMemory)
        {
            services.TryAddSingleton<IResultStore, InMemoryResultStore>();
        }
        // Redis será implementado posteriormente
        // else if (options.StorageType == StorageType.Redis)
        // {
        //     services.TryAddSingleton<IResultStore, RedisResultStore>();
        // }

        // Configura AI Provider
        ConfigureAiProvider(services, options);

        return services;
    }

    private static void ConfigureAiProvider(IServiceCollection services, FinDocAnalyserOptions options)
    {
        switch (options.AiProvider)
        {
            case AiProvider.OpenAI:
                services.AddSingleton<IChatClient>(sp =>
                {
                    var openAiClient = new OpenAI.OpenAIClient(options.OpenAI.ApiKey);
                    return openAiClient.AsChatClient(options.OpenAI.Model);
                });
                break;

            case AiProvider.AzureOpenAI:
                throw new NotImplementedException("Azure OpenAI será implementado em breve");

            case AiProvider.Ollama:
                throw new NotImplementedException("Ollama será implementado em breve");

            default:
                throw new NotSupportedException($"AI Provider '{options.AiProvider}' não suportado");
        }
    }
}

/// <summary>
/// Opções de configuração do FinDocAnalyser
/// </summary>
public class FinDocAnalyserOptions
{
    /// <summary>
    /// Provider de IA a ser usado
    /// </summary>
    public AiProvider AiProvider { get; set; } = AiProvider.OpenAI;

    /// <summary>
    /// Habilita cache de PDFs (SHA256)
    /// </summary>
    public bool EnablePdfCache { get; set; } = true;

    /// <summary>
    /// Habilita cache de respostas da IA
    /// </summary>
    public bool EnableAiCache { get; set; } = false;

    /// <summary>
    /// Habilita telemetria (OpenTelemetry)
    /// </summary>
    public bool EnableTelemetry { get; set; } = false;

    /// <summary>
    /// Tipo de storage para resultados
    /// </summary>
    public StorageType StorageType { get; set; } = StorageType.InMemory;

    /// <summary>
    /// Configurações da OpenAI
    /// </summary>
    public OpenAIOptions OpenAI { get; set; } = new();

    /// <summary>
    /// Configurações do Azure OpenAI
    /// </summary>
    public AzureOpenAIOptions AzureOpenAI { get; set; } = new();

    /// <summary>
    /// Configurações do Ollama (local)
    /// </summary>
    public OllamaOptions Ollama { get; set; } = new();

    /// <summary>
    /// String de conexão do Redis (se StorageType = Redis)
    /// </summary>
    public string? RedisConnectionString { get; set; }

    public void Validate()
    {
        if (AiProvider == AiProvider.OpenAI && string.IsNullOrEmpty(OpenAI.ApiKey))
            throw new InvalidOperationException("OpenAI API Key é obrigatória");

        if (AiProvider == AiProvider.AzureOpenAI && string.IsNullOrEmpty(AzureOpenAI.ApiKey))
            throw new InvalidOperationException("Azure OpenAI API Key é obrigatória");

        if (StorageType == StorageType.Redis && string.IsNullOrEmpty(RedisConnectionString))
            throw new InvalidOperationException("Redis connection string é obrigatória");
    }
}

public class OpenAIOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "gpt-4o";
}

public class AzureOpenAIOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty;
    public string DeploymentName { get; set; } = "gpt-4o";
}

public class OllamaOptions
{
    public string Endpoint { get; set; } = "http://localhost:11434";
    public string Model { get; set; } = "llama3.2";
}

public enum AiProvider
{
    OpenAI,
    AzureOpenAI,
    Ollama
}

public enum StorageType
{
    InMemory,
    Redis
}
