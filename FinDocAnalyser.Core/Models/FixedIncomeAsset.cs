using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FinDocAnalyzer.Core.Models;

public class FixedIncomePortfolio
{
    public decimal TotalInvested { get; set; }
    public string Currency { get; set; } = "BRL";
    public List<FixedIncomeAsset> Assets { get; set; } = new();
    
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

public class FixedIncomeAsset
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;  // LCI, CDB, Debênture, etc.
    public string Issuer { get; set; } = string.Empty;  // Emissor (banco ou instituição)
    public decimal? Quantity { get; set; }  // Quantidade de títulos/unidades
    public decimal InvestedAmount { get; set; }
    public decimal? UnitPrice { get; set; }  // Preço unitário original (InvestedAmount / Quantity)
    public decimal CurrentValue { get; set; }
    public decimal? CurrentUnitPrice { get; set; }  // Preço unitário atual (CurrentValue / Quantity)
    public decimal? AccruedInterest { get; set; }  // Juros acumulados
    public decimal? TotalValueWithAccruedInterest { get; set; }  // Valor total com juros acumulados
    public decimal? Return { get; set; }
    public decimal? ReturnPercentage { get; set; }
    public string Rate { get; set; } = string.Empty;  // "96% CDI", "IPCA +3.6%"
    public string Yield { get; set; } = string.Empty;  // Dividend yield ou yield atual, ex: "4.66%"
    public decimal Confidence { get; set; }
    public string ConfidenceReason { get; set; } = string.Empty;
}
