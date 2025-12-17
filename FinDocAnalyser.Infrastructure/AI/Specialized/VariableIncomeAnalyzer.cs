using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using FinDocAnalyzer.Core.Models;

namespace FinDocAnalyzer.Infrastructure.AI.Specialized;

/// <summary>
/// Specialized AI analyzer for Variable Income assets
/// </summary>
public class VariableIncomeAnalyzer : BaseSpecializedAnalyzer<VariableIncomePortfolio>
{
    public override string Category => "VariableIncome";
    
    public VariableIncomeAnalyzer(
        IChatClient chatClient,
        ILogger<VariableIncomeAnalyzer> logger)
        : base(chatClient, logger)
    {
    }
    
    protected override string CreateSpecializedPrompt()
    {
        return @"# Variable Income Portfolio Analyzer

You are a specialized AI trained to extract and interpret Variable Income assets from financial institution reports.

## Your Mission

1. EXPLORE the entire document to understand its structure
   - Read through all sections to identify variable income data
   - Look for labels like: 'Renda Variável', 'Variable Income', 'Equity Portfolio', 'Ações', 'Stocks', 'Equities', etc.

2. IDENTIFY all tables/sections related to variable income assets
   - Find tables showing individual assets (stocks, ETFs, etc.)
   - Distinguish between:
     * Detail tables (individual holdings with tickers/names)
     * Summary tables (aggregations by sector, totals, distributions)

3. ANALYZE and CHOOSE the most appropriate data source
   - Prioritize tables with INDIVIDUAL asset details (tickers, specific names)
   - Avoid tables that only show aggregated totals or sector distributions
   - If you see multiple tables, choose the one with the most granular detail
   - CRITICAL: If table has MULTIPLE PERIOD COLUMNS (e.g., ""Last Period"" and ""This Period""), extract ONLY values from the MOST RECENT column (""This Period"" / ""Current Period"")
   - Explain your choice in the confidenceReason field

4. EXTRACT ALL individual assets from the chosen source
   - Include EVERY asset listed (regardless of status, value, or condition)
   - Skip total/subtotal rows within the table
   - Asset type, value amount, or status should NOT filter out positions

   CRITICAL: Your job is to be a faithful mirror of individual assets in the report. Include all assets, even with problems or unusual status. Never filter based on quality.

5. INTERPRET the data structure intelligently
   - Identify columns: ticker, quantity, average price, current value, return, etc.
   - Parse tables, lists, or narrative text
   - Calculate totals if not explicitly stated
   - Be flexible with different document formats

## Output Schema

Return ONLY a valid JSON with no additional text.

JSON Schema:
{
  ""totalInvested"": number,
  ""currency"": ""string (BRL/USD/EUR)"",
  ""assets"": [
    {
      ""ticker"": ""string"",
      ""name"": ""string (optional)"",
      ""type"": ""string (Stock/ETF/FII/BDR/Option/Other)"",
      ""quantity"": number,
      ""averagePrice"": number,
      ""currentValue"": number,
      ""return"": number or null,
      ""returnPercentage"": number or null,
      ""yield"": ""string or null"",
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
3. If NO sections found, return empty arrays and zero totals
4. Include ALL assets found in the target sections
5. The section label defines the category, not the asset's semantic type
6. Confidence scoring:
   - 0.95-1.0: Clear structured data in tables
   - 0.85-0.94: Identifiable but requires interpretation
   - 0.70-0.84: Inferred from context
   - Below 0.70: Still include but flag as low confidence

Extract all assets with maximum precision!";
    }
    
    protected override VariableIncomePortfolio? ParseResponse(string jsonResponse)
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
                    "[VariableIncome] ⚠️ Validation: Sum mismatch! Assets sum: {Calculated:N2}, Reported total: {Reported:N2}, Diff: {Diff:N2} ({Percent:N2}%)",
                    calculatedSum, result.TotalContribution, difference, percentDiff);
                
                // Auto-correct: Use calculated sum as truth
                _logger.LogInformation("[VariableIncome] 🔧 Auto-correcting TotalContribution to {Corrected:N2}", calculatedSum);
                result.TotalContribution = calculatedSum;
            }
            else
            {
                _logger.LogInformation("[VariableIncome] ✅ Validation passed: Sum matches total ({Total:N2})", calculatedSum);
            }
        }
        
        return result;
    }
}
