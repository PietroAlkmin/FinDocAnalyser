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

1. LOCATE sections in the document with these labels (case-insensitive, multilingual):
   - 'Renda Variável'
   - 'Variable Income'
   - 'Equity Portfolio'
   - 'Ações'
   - 'Stocks'
   - 'Equities'
   - 'Stock Holdings'
   - 'Variable Rate'
   - Any similar variations

2. EXTRACT ALL assets within those sections, regardless of:
   - Asset type (if it's in the section, include it)
   - Value amount (include all positions, even zero or negative)
   - Asset status (include delisted, suspended, bankrupt, or any status)
   - Data completeness (use confidence scoring for partial data)

   CRITICAL: Your job is to be a faithful mirror of the report. Include EVERY asset listed in the section, even if it has problems, is delisted, or has unusual status. Never filter based on asset quality or condition.

3. INTERPRET the data structure:
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
        return ParseJsonResponse(jsonResponse);
    }
}
