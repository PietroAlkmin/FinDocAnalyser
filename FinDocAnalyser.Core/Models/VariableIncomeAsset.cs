using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FinDocAnalyzer.Core.Models;

/// <summary>
/// Variable Income Portfolio (stocks, equity ETFs, ADRs, BDRs, stock options)
/// </summary>
public class VariableIncomePortfolio
{
    public decimal TotalInvested { get; set; }
    public string Currency { get; set; } = "BRL";
    public List<VariableIncomeAsset> Assets { get; set; } = new();
    
    /// <summary>
    /// Total contribution to portfolio (sum of all asset currentValues)
    /// </summary>
    public decimal TotalContribution { get; set; }
    
    /// <summary>
    /// Percentage of total portfolio
    /// </summary>
    public decimal? PercentageOfPortfolio { get; set; }
}

/// <summary>
/// Individual Variable Income Asset
/// </summary>
public class VariableIncomeAsset
{
    /// <summary>
    /// Asset ticker/code (e.g.: "PETR4", "AAPL", "BOVA11")
    /// </summary>
    public string Ticker { get; set; } = string.Empty;

    /// <summary>
    /// Asset full name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Asset type (e.g.: "Stock", "ETF", "ADR", "BDR", "Stock Option")
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Quantity of shares/units (accepts fractional)
    /// </summary>
    public decimal Quantity { get; set; }

    /// <summary>
    /// Average purchase price
    /// </summary>
    public decimal AveragePrice { get; set; }

    /// <summary>
    /// Unit price at acquisition (InvestedAmount / Quantity)
    /// </summary>
    public decimal? UnitPrice { get; set; }

    /// <summary>
    /// Total current position value
    /// </summary>
    public decimal CurrentValue { get; set; }

    /// <summary>
    /// Current unit price (CurrentValue / Quantity)
    /// </summary>
    public decimal? CurrentUnitPrice { get; set; }

    /// <summary>
    /// Absolute return (profit/loss)
    /// </summary>
    public decimal? Return { get; set; }

    /// <summary>
    /// Percentage return
    /// </summary>
    public decimal? ReturnPercentage { get; set; }

    /// <summary>
    /// Dividend yield (e.g.: "8.62%")
    /// </summary>
    public string Yield { get; set; } = string.Empty;

    /// <summary>
    /// Extraction confidence (0.0 to 1.0)
    /// </summary>
    public decimal Confidence { get; set; }

    /// <summary>
    /// Confidence level explanation
    /// </summary>
    public string ConfidenceReason { get; set; } = string.Empty;
}
