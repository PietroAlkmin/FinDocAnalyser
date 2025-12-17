using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using FinDocAnalyzer.Core.Interfaces;
using FinDocAnalyzer.Core.Models;

namespace FinDocAnalyzer.Infrastructure.AI.Specialized;

/// <summary>
/// Aggregator AI that consolidates specialized portfolio results
/// </summary>
public class AggregatorAnalyzer : IAggregatorAnalyzer
{
    private readonly IChatClient _chatClient;
    private readonly ILogger<AggregatorAnalyzer> _logger;
    
    public AggregatorAnalyzer(
        IChatClient chatClient,
        ILogger<AggregatorAnalyzer> logger)
    {
        _chatClient = chatClient;
        _logger = logger;
    }
    
    public async Task<AggregatedResult?> AnalyzeAsync(string extractedText)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            _logger.LogInformation("[Aggregator] Starting independent analysis...");
            
            // Truncate text if too long
            var textToAnalyze = extractedText.Length > 120000
                ? extractedText[..120000]
                : extractedText;
            
            var prompt = CreateAggregatorPrompt();
            var userPrompt = $@"Analyze this financial report and extract ONLY Total Invested and Asset Classification:

{textToAnalyze}

Return a valid JSON following the defined schema.";
            
            var chatOptions = new ChatOptions
            {
                ResponseFormat = ChatResponseFormat.Json,
                Temperature = 0.1f,
                MaxOutputTokens = 2048
            };
            
            var messages = new List<ChatMessage>
            {
                new(ChatRole.System, prompt),
                new(ChatRole.User, userPrompt)
            };
            
            var response = await _chatClient.CompleteAsync(messages, chatOptions);
            stopwatch.Stop();
            
            var tokensUsed = response.Usage?.TotalTokenCount ?? 0;
            
            _logger.LogInformation(
                "[Aggregator] ✅ Analysis completed in {Duration}ms - Tokens: {Tokens}",
                stopwatch.ElapsedMilliseconds,
                tokensUsed);
            
            var result = ParseResponse(response.Message.Text ?? string.Empty);
            
            if (result == null)
            {
                _logger.LogWarning("[Aggregator] ⚠️ Parsed result is null");
            }
            
            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "[Aggregator] ❌ Analysis failed: {Message}", ex.Message);
            return null;
        }
    }
    
    private string CreateAggregatorPrompt()
    {
        return @"# Total Invested and Asset Classification Analyzer

You are an expert AI specialized in extracting overall portfolio totals and asset classification from financial institution reports.

## Your Mission

1. LOCATE summary sections in the document
   - Look for: 'Total Invested', 'Total Assets', 'Portfolio Total', 'Grand Total', 'Total Patrimônio', 'Total de Investimentos'
   - Find: 'Asset Allocation', 'Asset Classification', 'Classificação de Ativos', 'Distribution by Asset Type', 'Distribuição por Classe'

2. EXTRACT Total Invested
   - Find the GRAND TOTAL of all investments across all asset categories
   - Identify the currency (USD, BRL, EUR, etc.)
   - CRITICAL: If table has MULTIPLE PERIOD COLUMNS (e.g., ""Last Period"" and ""This Period""), extract ONLY values from the MOST RECENT column (""This Period"" / ""Current Period"")

3. EXTRACT Asset Classification
   - Find breakdown by major asset categories:
     * Variable Income / Renda Variável / Equities / Stocks
     * Fixed Income / Renda Fixa / Bonds / Fixed Rate
     * Alternative Assets / Alternativos / FIIs / REITs / Crypto
     * Cash / Caixa / Liquidity / Disponível
   - Extract invested amount and percentage for each category
   - Percentages should sum to approximately 100%

4. INTERPRET intelligently
   - Look for summary tables at the beginning or end of the report
   - Distinguish between individual asset tables vs. summary tables
   - Prioritize tables that show totals and percentages by category

## Output Schema

Return ONLY a valid JSON with no additional text.

JSON Schema:
{
  ""total"": {
    ""totalInvestedAmount"": number,
    ""currency"": ""string""
  },
  ""classification"": {
    ""totalInvested"": number,
    ""currency"": ""string"",
    ""classes"": [
      {
        ""assetClassName"": ""string (VariableIncome/FixedIncome/AlternativeAssets/Cash)"",
        ""invested"": number,
        ""percentage"": number,
        ""confidence"": number (0.0 to 1.0),
        ""confidenceReason"": ""string""
      }
    ]
  },
  ""updatedPercentages"": {
    ""variableIncomePercentage"": number or null,
    ""fixedIncomePercentage"": number or null,
    ""alternativeAssetsPercentage"": number or null,
    ""cashPercentage"": number or null
  },
  ""validationWarnings"": [""string""],
  ""hasInconsistencies"": boolean,
  ""reasoning"": {
    ""totalCalculation"": ""string"",
    ""currencyHandling"": ""string"",
    ""percentageValidation"": ""string"",
    ""anomaliesDetected"": [""string""]
  }
}

## Critical Rules

1. CRITICAL: Extract ONLY from the most recent period if multiple columns present
2. totalInvestedAmount should match the sum of all asset classes' invested amounts
3. Percentages should sum to 100% (±1% tolerance)
4. Use consistent currency across total and classification
5. If no summary table found, return null for both total and classification
6. Confidence scoring:
   - 0.95-1.0: Found in clear summary tables with percentages
   - 0.85-0.94: Found in summary but had to calculate percentages
   - 0.70-0.84: Inferred from multiple sections
   - Below 0.70: Return null (data too uncertain)

Be precise and extract only from summary sections - do NOT calculate by adding individual assets!";
    }
    
    private AggregatedResult? ParseResponse(string jsonResponse)
    {
        try
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            
            return JsonSerializer.Deserialize<AggregatedResult>(jsonResponse, options);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "[Aggregator] JSON parse error: {Message}", ex.Message);
            return null;
        }
    }
}
