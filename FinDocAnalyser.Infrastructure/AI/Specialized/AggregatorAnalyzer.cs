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
    
    public async Task<AggregatedResult> AggregateAsync(
        VariableIncomePortfolio? variableIncome,
        FixedIncomePortfolio? fixedIncome,
        AlternativeAssetsPortfolio? alternativeAssets,
        CashPortfolio? cash)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            _logger.LogInformation("[Aggregator] Starting aggregation and validation...");
            
            // Build input JSON for AI
            var inputData = new
            {
                variableIncome = variableIncome != null ? new
                {
                    totalContribution = variableIncome.TotalContribution,
                    currency = variableIncome.Currency,
                    assetsCount = variableIncome.Assets.Count
                } : null,
                fixedIncome = fixedIncome != null ? new
                {
                    totalContribution = fixedIncome.TotalContribution,
                    currency = fixedIncome.Currency,
                    assetsCount = fixedIncome.Assets.Count
                } : null,
                alternativeAssets = alternativeAssets != null ? new
                {
                    totalContribution = alternativeAssets.TotalContribution,
                    currency = alternativeAssets.Currency,
                    assetsCount = alternativeAssets.Assets.Count
                } : null,
                cash = cash != null ? new
                {
                    totalContribution = cash.TotalContribution,
                    currency = cash.Currency,
                    positionsCount = cash.Positions.Count
                } : null
            };
            
            var inputJson = JsonSerializer.Serialize(inputData, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            
            var prompt = CreateAggregatorPrompt();
            var userPrompt = $@"Aggregate and validate these specialized portfolio results:

{inputJson}

Calculate totals, classifications, percentages, and detect any inconsistencies.
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
                "[Aggregator] ✅ Aggregation completed in {Duration}ms - Tokens: {Tokens}",
                stopwatch.ElapsedMilliseconds,
                tokensUsed);
            
            var result = ParseResponse(response.Message.Text ?? string.Empty);
            
            if (result == null)
            {
                _logger.LogWarning("[Aggregator] ⚠️ Parsed result is null, using fallback");
                result = CreateFallbackResult(variableIncome, fixedIncome, alternativeAssets, cash);
            }
            
            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "[Aggregator] ❌ Aggregation failed: {Message}", ex.Message);
            
            // Fallback to programmatic aggregation
            _logger.LogWarning("[Aggregator] Using fallback programmatic aggregation");
            return CreateFallbackResult(variableIncome, fixedIncome, alternativeAssets, cash);
        }
    }
    
    private string CreateAggregatorPrompt()
    {
        return @"# Portfolio Aggregator and Validator

You are an expert AI specialized in consolidating and validating multi-portfolio financial analyses.

## Input Data

You will receive results from 4 specialized analyzers:
1. Variable Income - stocks, ETFs, and assets from 'Renda Variável' sections
2. Fixed Income - bonds, CDB, and assets from 'Renda Fixa' sections
3. Alternative Assets - FIIs, REITs, crypto from 'Alternativos' sections
4. Cash - checking, savings, liquidity from 'Caixa' sections

## Your Responsibilities

### 1. Calculate Total Invested
- Sum totalContribution from all 4 portfolios
- Handle multiple currencies (convert or document if needed)
- Return consolidated grand total

### 2. Generate Classification by Category
- Calculate percentage of each portfolio relative to grand total
- Validate that percentages sum to 100% (allow 0.5% tolerance for rounding)
- Identify dominant asset class

### 3. Cross-Validation
- Document negative values in reasoning (they are valid - may represent losses)
- Verify totalContribution matches sum of individual assets in each portfolio
- Flag if percentages are mathematically inconsistent
- Detect anomalies or outliers

CRITICAL: Never discard or filter portfolios or assets based on unusual values (negative, zero, or extreme). Your job is to consolidate and validate the data as provided by the specialized analyzers, being a faithful mirror of what they extracted. Include everything in calculations and classification.

### 4. Update Percentages
- Calculate correct percentageOfPortfolio for each category
- Ensure mathematical consistency across all values

### 5. Currency Handling
- If all portfolios use BRL, use BRL for total
- If all portfolios use USD, use USD for total
- If mixed currencies, use the most common one
- Document currency decisions and conversions in reasoning

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

1. If a portfolio is null (specialist failed), set its percentage to 0% and document in reasoning
2. Percentages must sum to 100% with max 0.5% tolerance
3. Never assume currency conversions - document if mixed currencies present
4. Validate that each portfolio's totalContribution equals sum of its assets
5. Flag any mathematical inconsistencies in validationWarnings
6. Provide clear reasoning for all decisions and adjustments
- Percentages MUST sum to 100% (±0.1% tolerance)
- Confidence = 1.0 if data is consistent, < 1.0 if there are problems
- Document ALL decisions in reasoning
- Detect anomalies: negative values, sum mismatches, impossible percentages

Be precise, consistent, and thorough in validations!";
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
    
    /// <summary>
    /// Fallback programmatic aggregation if AI fails
    /// </summary>
    private AggregatedResult CreateFallbackResult(
        VariableIncomePortfolio? variableIncome,
        FixedIncomePortfolio? fixedIncome,
        AlternativeAssetsPortfolio? alternativeAssets,
        CashPortfolio? cash)
    {
        var totalAmount = 0m;
        var classes = new List<AssetClass>();
        
        if (variableIncome != null && variableIncome.TotalContribution > 0)
        {
            totalAmount += variableIncome.TotalContribution;
            classes.Add(new AssetClass
            {
                AssetClassName = "Variable Income",
                Invested = variableIncome.TotalContribution,
                Percentage = 0,
                Confidence = 1.0m,
                ConfidenceReason = "Calculated from specialized AI"
            });
        }
        
        if (fixedIncome != null && fixedIncome.TotalContribution > 0)
        {
            totalAmount += fixedIncome.TotalContribution;
            classes.Add(new AssetClass
            {
                AssetClassName = "Fixed Income",
                Invested = fixedIncome.TotalContribution,
                Percentage = 0,
                Confidence = 1.0m,
                ConfidenceReason = "Calculated from specialized AI"
            });
        }
        
        if (alternativeAssets != null && alternativeAssets.TotalContribution > 0)
        {
            totalAmount += alternativeAssets.TotalContribution;
            classes.Add(new AssetClass
            {
                AssetClassName = "Alternative Assets",
                Invested = alternativeAssets.TotalContribution,
                Percentage = 0,
                Confidence = 1.0m,
                ConfidenceReason = "Calculated from specialized AI"
            });
        }
        
        if (cash != null && cash.TotalContribution > 0)
        {
            totalAmount += cash.TotalContribution;
            classes.Add(new AssetClass
            {
                AssetClassName = "Cash",
                Invested = cash.TotalContribution,
                Percentage = 0,
                Confidence = 1.0m,
                ConfidenceReason = "Calculated from specialized AI"
            });
        }
        
        // Calculate percentages
        foreach (var cls in classes)
        {
            cls.Percentage = totalAmount > 0 ? (cls.Invested / totalAmount) * 100 : 0;
        }
        
        var result = new AggregatedResult
        {
            Total = new TotalInvested
            {
                TotalInvestedAmount = totalAmount,
                Currency = "BRL" // Default
            },
            Classification = new AssetClassification
            {
                TotalInvested = totalAmount,
                Currency = "BRL",
                Classes = classes
            },
            UpdatedPercentages = new PortfolioPercentages
            {
                VariableIncomePercentage = classes.FirstOrDefault(c => c.AssetClassName == "Variable Income")?.Percentage,
                FixedIncomePercentage = classes.FirstOrDefault(c => c.AssetClassName == "Fixed Income")?.Percentage,
                AlternativeAssetsPercentage = classes.FirstOrDefault(c => c.AssetClassName == "Alternative Assets")?.Percentage,
                CashPercentage = classes.FirstOrDefault(c => c.AssetClassName == "Cash")?.Percentage
            },
            ValidationWarnings = new List<string> { "Used fallback programmatic aggregation (AI failed)" },
            HasInconsistencies = false
        };
        
        return result;
    }
}
