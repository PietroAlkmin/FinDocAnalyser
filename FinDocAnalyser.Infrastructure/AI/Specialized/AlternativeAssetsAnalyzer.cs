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

1. LOCATE sections in the document with these labels (case-insensitive, multilingual):
   - 'Fundos Imobiliários'
   - 'FIIs'
   - 'REITs'
   - 'Real Estate Funds'
   - 'Real Estate'
   - 'Criptomoedas'
   - 'Cryptocurrencies'
   - 'Crypto'
   - 'Digital Assets'
   - 'Ativos Alternativos'
   - 'Alternative Assets'
   - 'Alternative Investments'
   - 'Alternatives'
   - 'Private Equity'
   - 'Hedge Funds'
   - 'Commodities'
   - 'Structured Products'
   - Any similar variations

2. EXTRACT ALL assets within those sections, regardless of:
   - Asset type (if it's in the section, include it)
   - Value amount (include all positions, even zero or negative)
   - Asset status (include suspended, liquidated, defaulted, or any status)
   - Data completeness (use confidence scoring for partial data)

   CRITICAL: Your job is to be a faithful mirror of the report. Include EVERY asset listed in the section, even if it has problems, is suspended, or has unusual status. Never filter based on asset quality or condition.

3. INTERPRET the data structure:
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
      ""issuer"": ""string or null (fund manager or platform)"",
      ""description"": ""string or null"",
      ""quantity"": number or null,
      ""unitPrice"": number or null,
      ""investedAmount"": number or null,
      ""currentValue"": number,
      ""return"": number or null,
      ""returnPercentage"": number or null,
      ""yield"": ""string or null"",
      ""managementFee"": ""string or null"",
      ""lockupPeriod"": ""string or null"",
      ""inceptionDate"": ""string (YYYY-MM-DD) or null"",
      ""maturityDate"": ""string (YYYY-MM-DD) or null"",
      ""additionalData"": {{}} or null,
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
5. If NO sections found, return empty arrays and zero totals
6. Include ALL assets in the section
7. The section label defines the category, not semantic classification
8. additionalData allows maximum flexibility for unique fields

Extract all assets with maximum precision!";
    }
    
    protected override AlternativeAssetsPortfolio? ParseResponse(string jsonResponse)
    {
        return ParseJsonResponse(jsonResponse);
    }
}
