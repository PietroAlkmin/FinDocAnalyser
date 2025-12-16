using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FinDocAnalyzer.Core.Models;

/// <summary>
/// Cash Portfolio (available balance, money market, sweep accounts)
/// </summary>
public class CashPortfolio
{
    public decimal? TotalBalance { get; set; }
    public string Currency { get; set; } = "BRL";
    public List<CashPosition> Positions { get; set; } = new();
    
    /// <summary>
    /// Total contribution to portfolio (sum of all position balances)
    /// </summary>
    public decimal TotalContribution { get; set; }
    
    /// <summary>
    /// Percentage of total portfolio
    /// </summary>
    public decimal? PercentageOfPortfolio { get; set; }
}

/// <summary>
/// Individual cash position
/// </summary>
public class CashPosition
{
    /// <summary>
    /// Account/position name (e.g.: "Checking Account", "JPM Deposit Sweep", "Money Market Fund")
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Position type (e.g.: "Checking Account", "Savings", "Money Market", "Sweep Account")
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Financial institution
    /// </summary>
    public string Institution { get; set; } = string.Empty;

    /// <summary>
    /// Current balance
    /// </summary>
    public decimal Balance { get; set; }

    /// <summary>
    /// Yield/interest rate (if applicable, e.g.: "0.25%")
    /// </summary>
    public string Yield { get; set; } = string.Empty;

    /// <summary>
    /// Position currency
    /// </summary>
    public string Currency { get; set; } = "BRL";

    /// <summary>
    /// Whether it's immediately available balance
    /// </summary>
    public bool IsAvailable { get; set; } = true;

    /// <summary>
    /// Extraction confidence (0.0 to 1.0)
    /// </summary>
    public decimal Confidence { get; set; }

    /// <summary>
    /// Confidence level explanation
    /// </summary>
    public string ConfidenceReason { get; set; } = string.Empty;
}
