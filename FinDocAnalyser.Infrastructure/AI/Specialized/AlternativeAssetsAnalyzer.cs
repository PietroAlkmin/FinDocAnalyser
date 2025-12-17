using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using FinDocAnalyzer.Core.Models;

namespace FinDocAnalyzer.Infrastructure.AI.Specialized;

/// <summary>
/// Specialized AI analyzer for Alternative Assets
/// </summary>
public class AlternativeAssetsAnalyzer : BaseSpecializedAnalyzer<AlternativeAssetsPortfolio>
{
    public override string Category => "AlternativeAssets";
    
    public AlternativeAssetsAnalyzer(
        IChatClient chatClient,
        ILogger<AlternativeAssetsAnalyzer> logger)
        : base(chatClient, logger)
    {
    }
    
    protected override string CreateSpecializedPrompt()
    {
        return @"# Alternative Assets Portfolio Analyzer

You are a specialized AI trained to extract and interpret Alternative Assets from financial institution reports.

## Your Mission

1. EXPLORE the entire document to understand its structure
   - Read through all sections to identify alternative assets data
   - Look for labels like: 'Fundos Imobiliários', 'FIIs', 'REITs', 'Crypto', 'Alternative Assets', 'Private Equity', 'Hedge Funds', etc.

2. IDENTIFY all tables/sections related to alternative assets
   - Find tables showing individual assets (REITs, funds, crypto holdings, etc.)
   - Distinguish between:
     * Detail tables (individual holdings with names/tickers)
     * Summary tables (aggregations by type, totals, distributions)

3. ANALYZE and CHOOSE the most appropriate data source
   - Prioritize tables with INDIVIDUAL asset details (names, tickers, specific descriptions)
   - Avoid tables that only show aggregated totals or type distributions
   - If you see multiple tables, choose the one with the most granular detail
   - CRITICAL: If table has MULTIPLE PERIOD COLUMNS (e.g., ""Last Period"" and ""This Period""), extract ONLY values from the MOST RECENT column (""This Period"" / ""Current Period"")
   - Explain your choice in the confidenceReason field

4. EXTRACT ALL individual assets from the chosen source
   - Include EVERY asset listed (regardless of status, value, or condition)
   - Skip total/subtotal rows within the table
   - Asset type, value amount, or status should NOT filter out positions

   CRITICAL: Your job is to be a faithful mirror of individual assets in the report. Include all assets, even with problems or unusual status. Never filter based on quality.

5. INTERPRET the data structure intelligently
   - Identify columns: name/ticker, quantity, price, value, return, yield, etc.
   - Parse tables, lists, or narrative text
   - Be flexible with diverse asset types and their specific fields
   - Calculate totals if not explicitly stated

## Output Schema

Return ONLY a valid JSON with no additional text.

JSON Schema:
{
  ""totalInvested"": number,
  ""currency"": ""string"",
  ""assets"": [
    {
      ""name"": ""string"",
      ""type"": ""string (FII/REIT/Crypto/PrivateEquity/Commodity/Other)"",
      ""symbol"": ""string or null (ticker or code)"",
      ""description"": ""string or null"",
      ""quantity"": number or null,
      ""unitPrice"": number or null,
      ""investedAmount"": number or null,
      ""currentValue"": number,
      ""return"": number or null,
      ""returnPercentage"": number or null (calculated as: (return / investedAmount) * 100, e.g., 8.2 for 8.2%),
      ""yield"": ""string or null"",
      ""managementFee"": ""string or null"",
      ""lockupPeriod"": ""string or null"",
      ""inceptionDate"": ""string (YYYY-MM-DD) or null"",
      ""maturityDate"": ""string (YYYY-MM-DD) or null"",
      ""additionalData"": {{""key"": ""value as string""}} or null,
      ""confidence"": number (0.0 to 1.0),
      ""confidenceReason"": ""string""
    }
  ],
  ""totalContribution"": number,
  ""percentageOfPortfolio"": null
}

## Critical Rules

1. totalContribution MUST equal the sum of all assets' currentValue
2. percentageOfPortfolio is always null (calculated later by aggregator)
3. Be FLEXIBLE with 'type' field - accept ANY asset category found in the section
4. Use additionalData object for asset-specific fields that don't fit the standard schema
5. CRITICAL: additionalData values MUST be strings - convert numbers to strings (e.g., 123.45 → ""123.45"")
6. If NO sections found, return empty arrays and zero totals
7. Include ALL assets in the section
8. The section label defines the category, not semantic classification
9. additionalData allows maximum flexibility for unique fields

Extract all assets with maximum precision!";
    }
    
    protected override AlternativeAssetsPortfolio? ParseResponse(string jsonResponse)
    {
        var result = ParseJsonResponse(jsonResponse);
        
        // Post-processing validation: Verify sum of assets matches total
        if (result?.Assets != null && result.Assets.Any())
        {
            var calculatedSum = result.Assets.Sum(a => a.CurrentValue);
            var difference = Math.Abs(result.TotalContribution - calculatedSum);
            var percentDiff = result.TotalContribution > 0 
                ? (difference / result.TotalContribution * 100) 
                : 0;
            
            if (difference > 0.01m) // Tolerance: 1 cent
            {
                _logger.LogWarning(
                    "[AlternativeAssets] ⚠️ Validation: Sum mismatch! Assets sum: {Calculated:N2}, Reported total: {Reported:N2}, Diff: {Diff:N2} ({Percent:N2}%)",
                    calculatedSum, result.TotalContribution, difference, percentDiff);
                
                // Auto-correct: Use calculated sum as truth
                _logger.LogInformation("[AlternativeAssets] 🔧 Auto-correcting TotalContribution to {Corrected:N2}", calculatedSum);
                result.TotalContribution = calculatedSum;
            }
            else
            {
                _logger.LogInformation("[AlternativeAssets] ✅ Validation passed: Sum matches total ({Total:N2})", calculatedSum);
            }
        }
        
        return result;
    }
}
