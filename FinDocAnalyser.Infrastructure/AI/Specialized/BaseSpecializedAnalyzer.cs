using System;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using FinDocAnalyzer.Core.Interfaces;

namespace FinDocAnalyzer.Infrastructure.AI.Specialized;

/// <summary>
/// Base class for specialized AI analyzers
/// </summary>
public abstract class BaseSpecializedAnalyzer<TResult> : ISpecializedAnalyzer<TResult>
    where TResult : class
{
    protected readonly IChatClient _chatClient;
    protected readonly ILogger _logger;
    private const int MaxTextLength = 120000;
    
    public abstract string Category { get; }
    public virtual bool IsCritical => false; // Default: non-critical (partial failure allowed)
    
    protected BaseSpecializedAnalyzer(
        IChatClient chatClient,
        ILogger logger)
    {
        _chatClient = chatClient;
        _logger = logger;
    }
    
    public async Task<TResult?> AnalyzeAsync(string extractedText)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            _logger.LogInformation("[{Category}] Starting specialized analysis...", Category);
            _logger.LogInformation("[{Category}] Input text length: {Length} chars", Category, extractedText.Length);
            
            // Truncate text if too long
            var textToAnalyze = extractedText.Length > MaxTextLength
                ? extractedText[..MaxTextLength]
                : extractedText;
            
            if (extractedText.Length > MaxTextLength)
            {
                _logger.LogWarning("[{Category}] Text truncated from {Original} to {Truncated} chars", 
                    Category, extractedText.Length, MaxTextLength);
            }
            
            var prompt = CreateSpecializedPrompt();
            var userPrompt = $@"Analyze this financial report and extract ONLY {Category} data:

{textToAnalyze}

Return a valid JSON following the defined schema.";
            
            // AUDIT: Log first 500 chars of text being analyzed
            _logger.LogDebug("[{Category}] Text preview (first 500 chars): {Preview}...", 
                Category, 
                textToAnalyze.Length > 500 ? textToAnalyze[..500] : textToAnalyze);
            
            var chatOptions = new ChatOptions
            {
                ResponseFormat = ChatResponseFormat.Json,
                Temperature = 0.1f,
                MaxOutputTokens = 4096
            };
            
            var messages = new List<ChatMessage>
            {
                new(ChatRole.System, prompt),
                new(ChatRole.User, userPrompt)
            };
            
            _logger.LogInformation("[{Category}] Sending request to AI...", Category);
            var response = await _chatClient.CompleteAsync(messages, chatOptions);
            stopwatch.Stop();
            
            var tokensUsed = response.Usage?.TotalTokenCount ?? 0;
            
            _logger.LogInformation(
                "[{Category}] ✅ Analysis completed in {Duration}ms - Tokens: {Tokens}",
                Category,
                stopwatch.ElapsedMilliseconds,
                tokensUsed);
            
            var result = ParseResponse(response.Message.Text ?? string.Empty);
            
            if (result == null)
            {
                _logger.LogWarning("[{Category}] ⚠️ Parsed result is null", Category);
            }
            
            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "[{Category}] ❌ Analysis failed: {Message}", Category, ex.Message);
            
            if (IsCritical)
            {
                throw new InvalidOperationException($"Critical analyzer '{Category}' failed: {ex.Message}", ex);
            }
            
            // Partial failure - return null
            return null;
        }
    }
    
    /// <summary>
    /// Create specialized prompt for this analyzer
    /// </summary>
    protected abstract string CreateSpecializedPrompt();
    
    /// <summary>
    /// Parse AI response JSON into result type
    /// </summary>
    protected abstract TResult? ParseResponse(string jsonResponse);
    
    /// <summary>
    /// Default JSON parsing implementation
    /// </summary>
    protected TResult? ParseJsonResponse(string jsonResponse)
    {
        try
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            
            return JsonSerializer.Deserialize<TResult>(jsonResponse, options);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "[{Category}] JSON parse error: {Message}", Category, ex.Message);
            return null;
        }
    }
}
