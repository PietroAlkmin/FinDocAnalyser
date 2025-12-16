using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using FinDocAnalyzer.Core.Models;

namespace FinDocAnalyzer.Infrastructure.AI.Specialized;

/// <summary>
/// Specialized AI analyzer for Fixed Income assets
/// </summary>
public class FixedIncomeAnalyzer : BaseSpecializedAnalyzer<FixedIncomePortfolio>
{
    public override string Category => "FixedIncome";
    
    public FixedIncomeAnalyzer(
        IChatClient chatClient,
        ILogger<FixedIncomeAnalyzer> logger)
        : base(chatClient, logger)
    {
    }
    
    protected override string CreateSpecializedPrompt()
    {
        return @"# Fixed Income Portfolio Analyzer

You are a specialized AI trained to extract and interpret Fixed Income assets from financial institution reports.

## Your Mission

1. LOCATE sections in the document with these labels (case-insensitive, multilingual):
   - 'Renda Fixa'
   - 'Fixed Income'
   - 'US Fixed Income'
   - 'Non-US Fixed Income'
   - 'Global Fixed Income'
   - 'International Fixed Income'
   - 'Títulos'
   - 'Bonds'
   - 'Fixed Rate Investments'
   - 'Fixed Rate'
   - Any similar variations

2. EXTRACT ALL assets within those sections, regardless of:
   - Asset type (if it's in the section, include it)
   - Value amount (include all positions, even zero or negative)
   - Asset status (include defaulted, matured, failed, suspended, or any status)
   - Data completeness (use confidence scoring for partial data)

   CRITICAL: Your job is to be a faithful mirror of the report. Include EVERY asset listed in the section, even if it has problems, is defaulted, bankrupt, or has unusual status. Never filter based on asset quality or condition.

3. INTERPRET the data structure:
   - Identify columns: name, issuer, invested amount, current value, rate, maturity, etc.
   - Parse tables, lists, or narrative text
   - Distinguish between invested amount (cost basis) and current value when possible
   - Be flexible with different document formats

## Output Schema

Return ONLY a valid JSON with no additional text.

JSON Schema:
{
  ""totalInvested"": number,
  ""currency"": ""string"",
  ""assets"": [
    {
      ""name"": ""string"",
      ""type"": ""string (CDB/LCI/Treasury/Bond/MoneyMarket/Other)"",
      ""issuer"": ""string (bank or institution)"",
      ""investedAmount"": number or null,
      ""currentValue"": number,
      ""return"": number or null,
      ""returnPercentage"": number or null,
      ""rate"": ""string or null (e.g., 'CDI+2.5%' or '12.5% a.a.')"",
      ""yield"": ""string or null"",
      ""maturityDate"": ""string (YYYY-MM-DD) or null"",
      ""applicationDate"": ""string (YYYY-MM-DD) or null"",
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
3. investedAmount = original cost basis (look for 'Invested', 'Cost', 'Applied Amount')
4. currentValue = current market value (look for 'Current Value', 'Position', 'Balance')
5. If only one value available, use it for currentValue and set investedAmount to null
6. NEVER assume investedAmount equals currentValue
7. If NO sections found, return empty arrays and zero totals
8. Include ALL assets in the section
9. The section label defines the category, not the asset's semantic type

Extract all assets with maximum precision!";
    }
    
    protected override FixedIncomePortfolio? ParseResponse(string jsonResponse)
    {
        return ParseJsonResponse(jsonResponse);
    }
}
