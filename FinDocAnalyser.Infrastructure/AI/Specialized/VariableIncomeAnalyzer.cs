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

4. COUNT and EXTRACT ALL individual assets from the chosen source
   - COUNT the rows in the table: If section shows 12 stocks, return 12 stocks
   - Include EVERY row that represents an individual asset (skip only total/subtotal rows)
   - Include assets even if CurrentValue = 0 or CurrentValue = null
   - Include assets even if Quantity = 0 or missing
   - Include assets even if Return is negative or missing
   - Your response MUST have the same number of assets as the source table
   
   CRITICAL RULE: Number of assets in your response = Number of rows in source table (excluding totals)
   REMEMBER: You are a faithful mirror - if the table has 12 rows, your assets array must have 12 items.

5. INTERPRET the data structure intelligently
   - Identify columns: ticker, quantity, average price, current value, return, etc.
   - Parse tables, lists, or narrative text
   - Calculate totals if not explicitly stated
   - Be flexible with different document formats

## EDGE CASES & SPECIAL SITUATIONS

Handle these specific scenarios that may seem unusual but are CRITICAL:

1. **Assets with CurrentValue = 0.00**:
   - ALWAYS include assets where CurrentValue = 0.00 (zero)
   - These represent bankrupt companies, delisted stocks, or worthless positions
   - They are still part of the portfolio and MUST be reported
   - Example: OI S A with CurrentValue = 0.00 -> INCLUDE IT
   - Do NOT apply mental filters like relevance or active assets only

2. **Missing or Incomplete Data**:
   - Include assets even if some fields are null or empty
   - Use null for missing numeric values, empty string for missing text
   - Example: Asset with only Ticker and Quantity -> INCLUDE IT

3. **Negative Values**:
   - Include assets with negative CurrentValue (losses, impairments)
   - Do not filter based on profitability or positive values

4. **Suspended or Halted Trading**:
   - Include stocks with trading suspended
   - Include assets marked as halted or under review

CRITICAL: These are NOT exceptions to skip - they are MANDATORY inclusions. Count them as regular rows.

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
      ""unitPrice"": number or null (preço unitário original = investedAmount / quantity),
      ""currentValue"": number,
      ""currentUnitPrice"": number or null (preço unitário atual = currentValue / quantity),
      ""return"": number or null,
      ""returnPercentage"": number or null (calculated as: (return / investedAmount) * 100, e.g., 15.8 for 15.8%),
      ""yield"": ""string or null"",
      ""confidence"": number (0.0 to 1.0),
      ""confidenceReason"": ""string""
    }
  ],
  ""totalContribution"": number,
  ""percentageOfPortfolio"": null,
  ""thoughtProcess"": ""string - EXPLAIN YOUR COMPLETE REASONING: Which section did you find? Why did you choose this specific table? How did you identify individual stocks vs summary rows? What patterns did you observe? What assumptions did you make?""
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
