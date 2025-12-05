using FinDocAnalyzer.Core.Interfaces;
using FinDocAnalyzer.Core.Services;
using FinDocAnalyzer.Infrastructure.AI;
using FinDocAnalyzer.Infrastructure.Caching;
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
        services.AddScoped<IPdfExtractor, PdfPigExtractor>();
        services.AddScoped<IAiAnalyzer, AiDocumentAnalyzer>();

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
        services.AddSingleton<IChatClient>(sp =>
        {
            var azureOpenAIClient = new Azure.AI.OpenAI.AzureOpenAIClient(
                new Uri(options.AzureOpenAI.Endpoint),
                new System.ClientModel.ApiKeyCredential(options.AzureOpenAI.ApiKey));
            
            var chatClient = azureOpenAIClient.GetChatClient(options.AzureOpenAI.DeploymentName);
            
            return chatClient.AsChatClient();
        });
    }
}

/// <summary>
/// Opções de configuração do FinDocAnalyser
/// </summary>
public class FinDocAnalyserOptions
{
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
    /// Configurações do Azure OpenAI
    /// </summary>
    public AzureOpenAIOptions AzureOpenAI { get; set; } = new();

    /// <summary>
    /// String de conexão do Redis (se StorageType = Redis)
    /// </summary>
    public string? RedisConnectionString { get; set; }

    public void Validate()
    {
        if (string.IsNullOrEmpty(AzureOpenAI.ApiKey))
            throw new InvalidOperationException("Azure OpenAI API Key é obrigatória");

        if (string.IsNullOrEmpty(AzureOpenAI.Endpoint))
            throw new InvalidOperationException("Azure OpenAI Endpoint é obrigatório");

        if (StorageType == StorageType.Redis && string.IsNullOrEmpty(RedisConnectionString))
            throw new InvalidOperationException("Redis connection string é obrigatória");
    }
}

public class AzureOpenAIOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty;
    public string DeploymentName { get; set; } = "gpt-4o";
    public string ApiVersion { get; set; } = "2024-08-01-preview";
}

public enum StorageType
{
    InMemory,
    Redis
}
