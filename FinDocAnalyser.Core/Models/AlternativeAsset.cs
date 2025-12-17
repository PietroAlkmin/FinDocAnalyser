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
}

/// <summary>
/// Individual Alternative Asset - flexible model for various types of unconventional assets
/// </summary>
public class AlternativeAsset
{
    /// <summary>
    /// Asset/fund/investment name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Asset type/category (e.g.: "REIT", "FII", "Private Equity", "Hedge Fund", "Crypto", "Commodity", "Structured Product", "Art", "Venture Capital", etc)
    /// FLEXIBLE field - accepts any classification
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Symbol/ticker/identifier if available (e.g.: "BTC-USD", "O", "HGLG11")
    /// OPTIONAL - not all alternative assets have a ticker
    /// </summary>
    public string? Symbol { get; set; }

    /// <summary>
    /// Additional description or strategy (for complex funds)
    /// OPTIONAL - free field for extra information
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Quantity of units/shares/tokens
    /// OPTIONAL - not all assets have quantifiable units
    /// </summary>
    public decimal? Quantity { get; set; }

    /// <summary>
    /// Price/unit value (when applicable)
    /// OPTIONAL - for assets with price per unit
    /// </summary>
    public decimal? UnitPrice { get; set; }

    /// <summary>
    /// Invested amount/committed capital
    /// Main financial field - always try to extract
    /// </summary>
    public decimal InvestedAmount { get; set; }

    /// <summary>
    /// Estimated current value/NAV/market value
    /// </summary>
    public decimal CurrentValue { get; set; }

    /// <summary>
    /// Absolute return (profit/loss)
    /// </summary>
    public decimal? Return { get; set; }

    /// <summary>
    /// Percentage return
    /// </summary>
    public decimal? ReturnPercentage { get; set; }

    /// <summary>
    /// Yield/income/distributions (free format: "5.2%", "$120/month", etc)
    /// OPTIONAL - for assets that generate periodic income
    /// </summary>
    public string? Yield { get; set; }

    /// <summary>
    /// Administration fee or management fee
    /// OPTIONAL - important for funds
    /// </summary>
    public string? ManagementFee { get; set; }

    /// <summary>
    /// Lock-up or waiting period
    /// OPTIONAL - for investments with liquidity restrictions
    /// </summary>
    public string? LockupPeriod { get; set; }

    /// <summary>
    /// Inception/subscription/acquisition date
    /// </summary>
    public DateTime? InceptionDate { get; set; }

    /// <summary>
    /// Maturity/exit/liquidation date (if applicable)
    /// </summary>
    public DateTime? MaturityDate { get; set; }

    /// <summary>
    /// Additional data in key-value format for maximum flexibility
    /// Allows capturing specific fields for each asset type
    /// E.g.: {"Vintage": "2023", "Geography": "Global", "Strategy": "Long/Short Equity"}
    /// </summary>
    public Dictionary<string, string>? AdditionalData { get; set; }

    /// <summary>
    /// Extraction confidence (0.0 to 1.0)
    /// </summary>
    public decimal Confidence { get; set; }

    /// <summary>
    /// Confidence level explanation
    /// </summary>
    public string ConfidenceReason { get; set; } = string.Empty;
}
