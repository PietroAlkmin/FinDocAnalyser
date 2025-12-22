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

4. COUNT and EXTRACT ALL Fixed Income securities from the chosen source
   - COUNT the rows in the table: If section shows 7 bonds, return 7 bonds
   - Include EVERY row that represents an individual asset (skip only total/subtotal rows)
   - Include assets even if CurrentValue = 0 or CurrentValue = null
   - Include assets even if InvestedAmount = 0 or missing
   - Include assets even if Return is negative or missing
   - Include assets even if Rate, Yield, or Maturity are missing
   - Your response MUST have the same number of assets as the source table
   
   CRITICAL RULE: Number of assets in your response = Number of rows in source table (excluding totals)
   
   WHAT TO INCLUDE:
   - ✅ Bonds, Notes, Debentures, Treasury Securities, Corporate Bonds, Government Bonds
   - ✅ Any instrument with maturity date, coupon rate, or labeled as ""Fixed Income""
   - ✅ Assets with CurrentValue = 0 (zero value assets)
   - ✅ Assets with negative values
   - ✅ Assets with missing/incomplete data
   
   WHAT TO EXCLUDE:
   - ❌ Pure cash positions clearly labeled under ""Cash"" section (not ""Fixed Income"")
   - ❌ Checking/savings accounts without maturity
   
   REMEMBER: You are a faithful mirror - if Fixed Income table has 7 rows, your assets array must have 7 items.

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
      ""quantity"": number or null,
      ""investedAmount"": number or null,
      ""unitPrice"": number or null (preço unitário original = investedAmount / quantity),
      ""currentValue"": number,
      ""currentUnitPrice"": number or null (preço unitário atual = currentValue / quantity),
      ""accruedInterest"": number or null (juros acumulados),
      ""totalValueWithAccruedInterest"": number or null (valor total com juros),
      ""return"": number or null,
      ""returnPercentage"": number or null (calculated as: (return / investedAmount) * 100, e.g., 5.5 for 5.5%),
      ""rate"": ""string or null (e.g., 'CDI+2.5%' or '12.5% a.a.')"",
      ""yield"": ""string or null"",
      ""confidence"": number (0.0 to 1.0),
      ""confidenceReason"": ""string""
    }
  ],
  ""totalContribution"": number,
  ""percentageOfPortfolio"": null,
  ""thoughtProcess"": ""string - EXPLAIN YOUR COMPLETE REASONING: Which section did you find? Why did you choose this specific table? How did you identify individual bonds vs summary rows? What data quality issues did you encounter? What assumptions did you make?""
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
