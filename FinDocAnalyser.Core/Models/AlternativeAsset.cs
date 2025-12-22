using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FinDocAnalyzer.Core.Models;

/// <summary>
/// Alternative Assets Portfolio (REITs, FIIs, Private Equity, Hedge Funds, Crypto, Commodities)
/// </summary>
public class AlternativeAssetsPortfolio
{
    public decimal TotalInvested { get; set; }
    public string Currency { get; set; } = "USD";
    public List<AlternativeAsset> Assets { get; set; } = new();
    
    /// <summary>
    /// Total contribution to portfolio (sum of all asset currentValues)
    /// </summary>
    public decimal TotalContribution { get; set; }
    
    /// <summary>
    /// Percentage of total portfolio
    /// </summary>
    public decimal? PercentageOfPortfolio { get; set; }
    
    /// <summary>
    /// AI analyzer's thought process and reasoning for extraction decisions
    /// </summary>
    public string? ThoughtProcess { get; set; }
}

/// <summary>
/// Individual Alternative Asset - simplified essential model
/// </summary>
public class AlternativeAsset
{
    /// <summary>
    /// Asset/fund/investment name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Asset type/category (e.g.: "REIT", "FII", "Private Equity", "Hedge Fund", "Crypto")
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Quantity of units/shares/tokens
    /// </summary>
    public decimal? Quantity { get; set; }

    /// <summary>
    /// Invested amount/committed capital
    /// </summary>
    public decimal InvestedAmount { get; set; }

    /// <summary>
    /// Unit price at acquisition (InvestedAmount / Quantity)
    /// </summary>
    public decimal? UnitPrice { get; set; }

    /// <summary>
    /// Estimated current value/NAV/market value
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
    /// Yield/income/distributions
    /// </summary>
    public string? Yield { get; set; }

    /// <summary>
    /// Extraction confidence (0.0 to 1.0)
    /// </summary>
    public decimal Confidence { get; set; }

    /// <summary>
    /// Confidence level explanation
    /// </summary>
    public string ConfidenceReason { get; set; } = string.Empty;
}
