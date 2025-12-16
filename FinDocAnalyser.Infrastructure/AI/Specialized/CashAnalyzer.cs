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

1. LOCATE sections in the document with these labels (case-insensitive, multilingual):
   - 'Caixa'
   - 'Cash'
   - 'Liquidez'
   - 'Liquidity'
   - 'Disponível'
   - 'Available'
   - 'Available Balance'
   - 'Cash Positions'
   - 'Cash & Equivalents'
   - 'Saldo Disponível'
   - Any similar variations

2. EXTRACT ALL positions within those sections, regardless of:
   - Account type (if it's in the section, include it)
   - Balance amount (include all positions, even zero or negative)
   - Account status (include closed, restricted, frozen, or any status)
   - Data completeness (use confidence scoring for partial data)

   CRITICAL: Your job is to be a faithful mirror of the report. Include EVERY position listed in the section, even if it has problems, is closed, or has unusual status. Never filter based on account condition.

3. INTERPRET the data structure:
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
