using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using FinDocAnalyzer.Core.Models;

namespace FinDocAnalyzer.Infrastructure.AI.Specialized;

/// <summary>
/// Specialized AI analyzer for Cash positions
/// </summary>
public class CashAnalyzer : BaseSpecializedAnalyzer<CashPortfolio>
{
    public override string Category => "Cash";
    
    public CashAnalyzer(
        IChatClient chatClient,
        ILogger<CashAnalyzer> logger)
        : base(chatClient, logger)
    {
    }
    
    protected override string CreateSpecializedPrompt()
    {
        return @"# Cash Positions Analyzer

You are a specialized AI trained to extract and interpret Cash positions from financial institution reports.

## Your Mission

1. EXPLORE the entire document to understand its structure
   - Read through all sections to identify cash-related data
   - Look for sections titled: 'Investment Positions', 'Posições de Investimento', '{Asset Class} Positions', '{Asset Class} Detailed', etc.
   - Within these sections, find subsections labeled: 'Cash', 'Caixa', 'Liquidity', 'Liquidez', 'Disponível', etc.

2. IDENTIFY tables showing INDIVIDUAL CASH POSITIONS/ASSETS
   - Your target: Tables listing individual cash assets/instruments (one per row)
   - Each row should represent ONE specific cash position (account, money market fund, sweep account, etc.)
   - Distinguish between:
     * ASSET POSITION tables: Each row = one distinct cash asset with specific name/identifier
     * SUMMARY/OVERVIEW tables: Each row = aggregation/category/account type grouping
   
   CRITICAL - UNDERSTAND WHAT YOU ARE LOOKING FOR:
   - Extract from: Sections labeled Investment Positions, Cash Positions, Detailed Holdings, etc.
   - Extract from: Tables where each row has a UNIQUE asset name/identifier
   - Extract from: Tables nested under your specific asset class (Cash/Caixa)
   - Skip: Tables showing account type distributions (categories, not assets)
   - Skip: Overview tables with generic groupings
   
   KEY PRINCIPLE: You are looking for INDIVIDUAL ASSETS within the Cash class, not categories or summaries.

3. ANALYZE and CHOOSE the most appropriate data source
   - Prioritize tables under Investment Positions or similar headings
   - Look for tables nested within Cash/Liquidity subsections
   - Each row should represent ONE specific cash asset/instrument
   - Avoid tables showing distributions, categorizations, or account type summaries
   - CRITICAL: If table has MULTIPLE PERIOD COLUMNS (e.g., Last Period and This Period), extract ONLY values from the MOST RECENT column (This Period / Current Period)
   - If NO table with individual cash asset positions exists, return EMPTY positions array
   - Explain your choice in the confidenceReason field

4. COUNT and EXTRACT ALL individual cash assets from the chosen source
   - COUNT the rows in the table: If section shows 5 cash positions, return 5 positions
   - Include EVERY row that represents an individual asset (skip only total/subtotal rows)
   - Include assets even if Balance = 0 or Balance = null
   - Include assets even if missing Institution or other fields
   - Your response MUST have the same number of positions as the source table
   
   CRITICAL RULE: Number of assets in your response = Number of rows in source table (excluding totals)
   REMEMBER: You are a faithful mirror - if the table has it, you must include it.

5. INTERPRET the data structure intelligently
   - Identify columns: account name, institution, balance, currency, etc.
   - Parse tables, lists, or narrative text
   - Determine if balance is immediately available or restricted
   - Be flexible with different document formats

## Output Schema

Return ONLY a valid JSON with no additional text.

JSON Schema:
{
  ""totalBalance"": number or null,
  ""currency"": ""string"",
  ""positions"": [
    {
      ""name"": ""string"",
      ""type"": ""string (CheckingAccount/SavingsAccount/MoneyMarket/SweepAccount/Other)"",
      ""institution"": ""string (bank or broker)"",
      ""balance"": number,
      ""yield"": ""string or null"",
      ""currency"": ""string"",
      ""isAvailable"": boolean,
      ""confidence"": number (0.0 to 1.0),
      ""confidenceReason"": ""string""
    }
  ],
  ""totalContribution"": number,
  ""percentageOfPortfolio"": null,
  ""thoughtProcess"": ""string - EXPLAIN YOUR COMPLETE REASONING: Which section did you find? Why did you choose this specific table? How did you distinguish between individual assets vs summary tables? What challenges did you face? What assumptions did you make?""
}
}

## Critical Rules

1. totalBalance MUST equal the sum of all positions' balance
2. totalContribution MUST equal totalBalance
3. percentageOfPortfolio is always null (calculated later by aggregator)
4. If NO sections found, return empty arrays and zero totals
5. Include ALL positions in the section
6. The section label defines the category
7. isAvailable = true for immediately accessible balances
8. isAvailable = false for restricted or locked cash

Extract all cash positions with maximum precision!";
    }
    
    protected override CashPortfolio? ParseResponse(string jsonResponse)
    {
        var result = ParseJsonResponse(jsonResponse);
        
        // Post-processing validation: Verify sum of positions matches total
        if (result?.Positions != null && result.Positions.Any())
        {
            var calculatedSum = result.Positions.Sum(p => p.Balance);
            var difference = Math.Abs(result.TotalContribution - calculatedSum);
            var percentDiff = result.TotalContribution > 0 
                ? (difference / result.TotalContribution * 100) 
                : 0;
            
            if (difference > 0.01m) // Tolerance: 1 cent
            {
                _logger.LogWarning(
                    "[Cash] WARNING - Validation: Sum mismatch! Positions sum: {Calculated:N2}, Reported total: {Reported:N2}, Diff: {Diff:N2} ({Percent:N2}%)",
                    calculatedSum, result.TotalContribution, difference, percentDiff);
                
                // Auto-correct: Use calculated sum as truth
                _logger.LogInformation("[Cash] Auto-correcting TotalContribution to {Corrected:N2}", calculatedSum);
                result.TotalContribution = calculatedSum;
            }
            else
            {
                _logger.LogInformation("[Cash] Validation passed: Sum matches total ({Total:N2})", calculatedSum);
            }
        }
        
        return result;
    }
}
