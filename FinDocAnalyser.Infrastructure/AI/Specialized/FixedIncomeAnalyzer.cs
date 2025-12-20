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
     * Detail tables (individual securities with names)
     * Summary tables (aggregations by type, maturity, totals)

3. ANALYZE and COMBINE data from ALL fixed income sections
   - Prioritize tables with INDIVIDUAL security details (names, specific descriptions)
   - Avoid tables that only show aggregated totals or type distributions
   - CRITICAL: If there are MULTIPLE SECTIONS (e.g., ""US Fixed Income"", ""Non-US Fixed Income"", ""Global Fixed Income""), YOU MUST EXTRACT FROM ALL OF THEM
   - CRITICAL: If table has MULTIPLE PERIOD COLUMNS (e.g., ""Last Period"" and ""This Period""), extract ONLY values from the MOST RECENT column (""This Period"" / ""Current Period"")
   - Explain your choice in the confidenceReason field

4. EXTRACT ONLY Fixed Income securities (BONDS, NOTES, DEBENTURES, etc.)
   - Include ALL Fixed Income assets listed (regardless of status, value, or condition)
   - Skip total/subtotal rows within the table
   - Asset type, value amount, or status should NOT filter out positions
   - **YOU ARE A MIRROR OF THE REPORT**: Extract EVERY individual asset you see in Fixed Income sections

   CRITICAL: Your job is to extract FIXED INCOME INSTRUMENTS ONLY:
   - ✅ Include: Bonds, Notes, Debentures, Treasury Securities, Fixed Rate Securities, Corporate Bonds, Government Bonds
   - ✅ Include: Any instrument with a maturity date, coupon rate, or labeled as ""Fixed Income""
   - ✅ Include: ""Falido"" (defaulted), ""Vencido"" (matured), ""Liquidado"" (liquidated), ""Suspenso"" (suspended)
   - ✅ Include: Negative values, ZERO values (CurrentValue = 0), expired securities
   - ✅ Include: Assets with missing data (no rate, no maturity, no dates)
   - ❌ EXCLUDE: Pure cash positions, bank deposits, checking/savings accounts, sweep accounts, or simple liquidity holdings
   - ❌ EXCLUDE: Assets that are clearly labeled as ""Cash"" category (not ""Fixed Income"")
   
   KEY DISTINCTION: Fixed Income = Debt instruments with interest/yield. Cash = Liquid deposits without maturity.
   If an asset is in a ""Fixed Income"" section but looks like cash, analyze its nature: Does it have a coupon/maturity? Include it. Is it just a deposit? Exclude it.
   
   REMEMBER: Even if CurrentValue is ZERO or negative, INCLUDE IT. You are a faithful mirror of all data present in the Fixed Income sections.

5. INTERPRET the data structure intelligently
   - Identify columns: name, invested amount, current value, rate, maturity, etc.
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
      ""returnPercentage"": number or null (calculated as: (return / investedAmount) * 100, e.g., 5.5 for 5.5%),
      ""rate"": ""string or null (e.g., 'CDI+2.5%' or '12.5% a.a.')"",
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
        var result = ParseJsonResponse(jsonResponse);
        
        // Post-processing validation: Calculate and verify totals
        if (result?.Assets != null && result.Assets.Any())
        {
            // Calculate both TotalInvested and TotalContribution from assets
            var calculatedInvested = result.Assets.Sum(a => a.InvestedAmount);
            var calculatedCurrent = result.Assets.Sum(a => a.CurrentValue);
            
            // Validate TotalContribution (CurrentValue sum)
            var difference = Math.Abs(result.TotalContribution - calculatedCurrent);
            var percentDiff = result.TotalContribution > 0 
                ? (difference / result.TotalContribution * 100) 
                : 0;
            
            if (difference > 0.01m) // Tolerance: 1 cent
            {
                _logger.LogWarning(
                    "[FixedIncome] ⚠️ Validation: CurrentValue sum mismatch! Assets sum: {Calculated:N2}, Reported total: {Reported:N2}, Diff: {Diff:N2} ({Percent:N2}%)",
                    calculatedCurrent, result.TotalContribution, difference, percentDiff);
                
                _logger.LogInformation("[FixedIncome] 🔧 Auto-correcting TotalContribution to {Corrected:N2}", calculatedCurrent);
                result.TotalContribution = calculatedCurrent;
            }
            else
            {
                _logger.LogInformation("[FixedIncome] ✅ Validation passed: CurrentValue sum matches total ({Total:N2})", calculatedCurrent);
            }
            
            // Always set TotalInvested from calculated sum
            _logger.LogInformation("[FixedIncome] 📊 Setting TotalInvested to {Invested:N2} (sum of InvestedAmount)", calculatedInvested);
            result.TotalInvested = calculatedInvested;
        }
        
        return result;
    }
}
