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

1. EXPLORE the entire document to understand its structure
   - Read through all sections to identify fixed income data
   - Look for labels like: 'Renda Fixa', 'Fixed Income', 'US Fixed Income', 'Non-US Fixed Income', 'Global Fixed Income', 'International Fixed Income', 'Títulos', 'Bonds', 'Fixed Rate', etc.
   - CRITICAL: Include ALL variations of Fixed Income sections (US, Non-US, Global, International, Domestic, Foreign)

2. IDENTIFY all tables/sections related to fixed income assets
   - Find ALL tables showing individual securities (bonds, CDBs, notes, etc.)
   - Look for MULTIPLE sections with different geographic labels (US, Non-US, Global, International)
   - Each section may contain separate tables - EXTRACT FROM ALL OF THEM
   - Distinguish between:
     * Detail tables (individual securities with names/issuers)
     * Summary tables (aggregations by type, maturity, totals)

3. ANALYZE and COMBINE data from ALL fixed income sections
   - Prioritize tables with INDIVIDUAL security details (names, issuers, specific descriptions)
   - Avoid tables that only show aggregated totals or type distributions
   - CRITICAL: If there are MULTIPLE SECTIONS (e.g., ""US Fixed Income"", ""Non-US Fixed Income"", ""Global Fixed Income""), YOU MUST EXTRACT FROM ALL OF THEM
   - CRITICAL: If table has MULTIPLE PERIOD COLUMNS (e.g., ""Last Period"" and ""This Period""), extract ONLY values from the MOST RECENT column (""This Period"" / ""Current Period"")
   - Explain your choice in the confidenceReason field

4. EXTRACT ALL individual securities from the chosen source
   - Include EVERY asset listed (regardless of status, value, or condition)
   - Skip total/subtotal rows within the table
   - Asset type, value amount, or status should NOT filter out positions

   CRITICAL: Your job is to be a faithful mirror of individual securities in the report. Include ALL assets with ANY status:
   - ✅ Include: ""Falido"" (defaulted), ""Vencido"" (matured), ""Liquidado"" (liquidated), ""Suspenso"" (suspended)
   - ✅ Include: Negative values, zero values, expired securities
   - ✅ Include: Assets with no maturity date, no rate, or missing data
   - ❌ NEVER filter based on quality, status, or value
   - ❌ NEVER exclude assets because they seem problematic
   
   Extract EVERYTHING you see in the table, even if it looks wrong or unusual.

5. INTERPRET the data structure intelligently
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
        // AUDIT: Log the raw JSON response from AI
        _logger.LogInformation("========== FIXED INCOME ANALYZER AUDIT ==========");
        _logger.LogInformation("RAW AI RESPONSE (JSON):");
        _logger.LogInformation("{JsonResponse}", jsonResponse);
        _logger.LogInformation("=================================================");
        
        var result = ParseJsonResponse(jsonResponse);
        
        if (result != null)
        {
            // AUDIT: Log extracted assets details
            _logger.LogInformation("========== FIXED INCOME ASSETS EXTRACTED ==========");
            _logger.LogInformation("Total Assets Found: {Count}", result.Assets?.Count ?? 0);
            _logger.LogInformation("Total Invested: {Total} {Currency}", result.TotalInvested, result.Currency);
            _logger.LogInformation("Total Contribution: {Total}", result.TotalContribution);
            
            if (result.Assets != null && result.Assets.Any())
            {
                foreach (var asset in result.Assets)
                {
                    _logger.LogInformation("  - Asset: {Name} | Issuer: {Issuer} | Type: {Type}", 
                        asset.Name ?? "N/A", 
                        asset.Issuer ?? "N/A", 
                        asset.Type ?? "N/A");
                    _logger.LogInformation("    Invested: {Invested} | Current: {Current} | Return: {Return}%", 
                        asset.InvestedAmount.ToString("N2"),
                        asset.CurrentValue.ToString("N2"),
                        asset.ReturnPercentage?.ToString("N2") ?? "N/A");
                    _logger.LogInformation("    Rate: {Rate} | Maturity: {Maturity}", 
                        asset.Rate ?? "N/A", 
                        asset.MaturityDate?.ToString("yyyy-MM-dd") ?? "N/A");
                    _logger.LogInformation("    Confidence: {Confidence:N2} - {Reason}", 
                        asset.Confidence, 
                        asset.ConfidenceReason ?? "N/A");
                }
            }
            _logger.LogInformation("===================================================");
        }
        
        return result;
    }
}
