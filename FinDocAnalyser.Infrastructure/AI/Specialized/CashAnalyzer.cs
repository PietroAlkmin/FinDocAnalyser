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
   - Look for labels like: 'Caixa', 'Cash', 'Liquidez', 'Liquidity', 'Disponível', 'Available Balance', 'Cash Positions', etc.

2. IDENTIFY all tables/sections related to cash positions
   - Find tables showing individual accounts or cash instruments
   - Distinguish between:
     * Detail tables (individual positions with specific names/descriptions)
     * Summary tables (aggregations by currency, totals, distributions)

3. ANALYZE and CHOOSE the most appropriate data source
   - Prioritize tables with INDIVIDUAL position details (account names, instrument descriptions)
   - Avoid tables that only show aggregated totals or currency distributions
   - If you see multiple tables, choose the one with the most granular detail
   - CRITICAL: If table has MULTIPLE PERIOD COLUMNS (e.g., ""Last Period"" and ""This Period""), extract ONLY values from the MOST RECENT column (""This Period"" / ""Current Period"")
   - Explain your choice in the confidenceReason field

4. EXTRACT ALL individual positions from the chosen source
   - Include EVERY position listed (regardless of status, amount, or condition)
   - Skip total/subtotal rows within the table
   - Account type, balance amount, or status should NOT filter out positions

   CRITICAL: Your job is to be a faithful mirror of individual positions in the report. Include all positions, even with problems or unusual status. Never filter based on quality.

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
  ""percentageOfPortfolio"": null
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
        return ParseJsonResponse(jsonResponse);
    }
}
