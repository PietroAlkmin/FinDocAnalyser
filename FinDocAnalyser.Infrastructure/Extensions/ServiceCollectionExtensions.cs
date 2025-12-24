using FinDocAnalyzer.Core.Interfaces;
using FinDocAnalyzer.Core.Models;
using FinDocAnalyzer.Core.Services;
using FinDocAnalyzer.Infrastructure.AI;
using FinDocAnalyzer.Infrastructure.AI.Specialized;
using FinDocAnalyzer.Infrastructure.Caching;
using FinDocAnalyzer.Infrastructure.Export;
using FinDocAnalyzer.Infrastructure.Pdf;
using FinDocAnalyzer.Infrastructure.Storage;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FinDocAnalyzer.Extensions;

/// <summary>
/// Extension methods to configure FinDocAnalyser SDK
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Add FinDocAnalyser services with default configuration
    /// </summary>
    public static IServiceCollection AddFinDocAnalyser(
        this IServiceCollection services,
        Action<FinDocAnalyserOptions> configure)
    {
        var options = new FinDocAnalyserOptions();
        configure(options);

        // Validate configuration
        options.Validate();

        // Register core services
        services.AddScoped<AnalysisOrchestrator>();
        services.AddScoped<IPdfExtractor, PdfPigExtractor>();
        services.AddScoped<IAiAnalyzer, AiDocumentAnalyzer>(); // Legacy analyzer (kept for backward compatibility)
        
        // Register specialized analyzers (new chain)
        services.AddScoped<ISpecializedAnalyzer<VariableIncomePortfolio>, VariableIncomeAnalyzer>();
        services.AddScoped<ISpecializedAnalyzer<FixedIncomePortfolio>, FixedIncomeAnalyzer>();
        services.AddScoped<ISpecializedAnalyzer<AlternativeAssetsPortfolio>, AlternativeAssetsAnalyzer>();
        services.AddScoped<ISpecializedAnalyzer<CashPortfolio>, CashAnalyzer>();
        services.AddScoped<IAggregatorAnalyzer, AggregatorAnalyzer>();
        services.AddScoped<ISpecializedAnalyzer<MovementsAnalysis>, MovementAnalyzer>();
        
        // Export services
        services.AddScoped<IExcelExporter, ExcelExporter>();

        // PDF cache
        if (options.EnablePdfCache)
        {
            services.TryAddSingleton<IPdfCache, InMemoryPdfCache>();
        }

        // Storage (default: InMemory)
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
/// FinDocAnalyser configuration options
/// </summary>
public class FinDocAnalyserOptions
{
    /// <summary>
    /// Enable PDF caching (SHA256)
    /// </summary>
    public bool EnablePdfCache { get; set; } = true;

    /// <summary>
    /// Enable AI response caching
    /// </summary>
    public bool EnableAiCache { get; set; } = false;

    /// <summary>
    /// Enable telemetry (OpenTelemetry)
    /// </summary>
    public bool EnableTelemetry { get; set; } = false;

    /// <summary>
    /// Storage type for results
    /// </summary>
    public StorageType StorageType { get; set; } = StorageType.InMemory;

    /// <summary>
    /// Azure OpenAI configuration
    /// </summary>
    public AzureOpenAIOptions AzureOpenAI { get; set; } = new();

    /// <summary>
    /// Redis connection string (if StorageType = Redis)
    /// </summary>
    public string? RedisConnectionString { get; set; }

    public void Validate()
    {
        if (string.IsNullOrEmpty(AzureOpenAI.ApiKey))
            throw new InvalidOperationException("Azure OpenAI API Key is required");

        if (string.IsNullOrEmpty(AzureOpenAI.Endpoint))
            throw new InvalidOperationException("Azure OpenAI Endpoint is required");

        if (StorageType == StorageType.Redis && string.IsNullOrEmpty(RedisConnectionString))
            throw new InvalidOperationException("Redis connection string is required");
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
