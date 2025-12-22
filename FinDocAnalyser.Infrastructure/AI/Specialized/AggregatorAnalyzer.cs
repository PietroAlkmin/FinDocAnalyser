using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.AI;
using AI = Microsoft.Extensions.AI;
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
            
            var messages = new List<Microsoft.Extensions.AI.ChatMessage>
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
   - Find breakdown by major asset categories (MUST include ALL 4 categories):
     * Variable Income / Renda Variável / Equities / Stocks
     * Fixed Income / Renda Fixa / Bonds / Fixed Rate
     * Alternative Assets / Alternativos / FIIs / REITs / Crypto
     * Cash / Caixa / Liquidity / Disponível / Cash Position / Liquidez
   - CRITICAL: ALWAYS look for CASH/LIQUIDITY category - it is often listed separately as ""Cash"", ""Caixa"", ""Disponível"", ""Liquidity"", or similar
   - Extract invested amount and percentage for each category
   - Percentages should sum to approximately 100%
   - If Cash is not explicitly shown in the summary table, check if there is a difference between the total and the sum of other categories - that difference is likely Cash

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
2. CRITICAL: ALWAYS include ALL 4 asset classes (VariableIncome, FixedIncome, AlternativeAssets, Cash) in the classification
3. CRITICAL: If Cash is not explicitly labeled in the table, calculate it as: Total - (VariableIncome + FixedIncome + AlternativeAssets)
4. totalInvestedAmount should match the sum of all asset classes' invested amounts
5. Percentages should sum to 100% (±1% tolerance)
6. Use consistent currency across total and classification
7. If no summary table found, return null for both total and classification
8. Confidence scoring:
   - 0.95-1.0: Found in clear summary tables with percentages
   - 0.85-0.94: Found in summary but had to calculate percentages or Cash
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
            
            var result = JsonSerializer.Deserialize<AggregatedResult>(jsonResponse, options);
            
            // Post-processing validation: Verify sums
            if (result?.Classification?.Classes != null && result.Classification.Classes.Any())
            {
                var calculatedSum = result.Classification.Classes.Sum(c => c.Invested);
                var reportedTotal = result.Classification.TotalInvested;
                var difference = Math.Abs(reportedTotal - calculatedSum);
                var percentDiff = reportedTotal > 0 
                    ? (difference / reportedTotal * 100) 
                    : 0;
                
                if (difference > 0.01m) // Tolerance: 1 cent
                {
                    _logger.LogWarning(
                        "[Aggregator] ⚠️ Validation: Classes sum mismatch! Calculated: {Calculated:N2}, Reported: {Reported:N2}, Diff: {Diff:N2} ({Percent:N2}%)",
                        calculatedSum, reportedTotal, difference, percentDiff);
                    
                    // Auto-correct: Use calculated sum
                    _logger.LogInformation("[Aggregator] 🔧 Auto-correcting TotalInvested to {Corrected:N2}", calculatedSum);
                    result.Classification.TotalInvested = calculatedSum;
                }
                else
                {
                    _logger.LogInformation("[Aggregator] ✅ Validation passed: Classes sum matches total ({Total:N2})", calculatedSum);
                }
                
                // Validate percentages sum to 100%
                var percentageSum = result.Classification.Classes.Sum(c => c.Percentage);
                var percentageDiff = Math.Abs(100m - percentageSum);
                
                if (percentageDiff > 1m) // Tolerance: 1%
                {
                    _logger.LogWarning(
                        "[Aggregator] ⚠️ Validation: Percentages sum to {Sum:N2}% (expected 100%)",
                        percentageSum);
                }
                else
                {
                    _logger.LogInformation("[Aggregator] ✅ Percentages validation passed: {Sum:N2}%", percentageSum);
                }
            }
            
            return result;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "[Aggregator] JSON parse error: {Message}", ex.Message);
            return null;
        }
    }
}
