using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using FinDocAnalyzer.Core.Models;

namespace FinDocAnalyzer.Core.Interfaces;

/// <summary>
/// Aggregator AI that consolidates specialized portfolio results
/// </summary>
public interface IAggregatorAnalyzer
{
    /// <summary>
    /// Aggregates specialized portfolios into total and classification
    /// </summary>
    /// <param name="variableIncome">Variable income portfolio (can be null)</param>
    /// <param name="fixedIncome">Fixed income portfolio (can be null)</param>
    /// <param name="alternativeAssets">Alternative assets portfolio (can be null)</param>
    /// <param name="cash">Cash portfolio (can be null)</param>
    /// <returns>Aggregated result with total, classification, and validations</returns>
    Task<AggregatedResult> AggregateAsync(
        VariableIncomePortfolio? variableIncome,
        FixedIncomePortfolio? fixedIncome,
        AlternativeAssetsPortfolio? alternativeAssets,
        CashPortfolio? cash);
}

/// <summary>
/// Result of aggregation analysis
/// </summary>
public class AggregatedResult
{
    public TotalInvested Total { get; set; } = new();
    public AssetClassification Classification { get; set; } = new();
    public PortfolioPercentages UpdatedPercentages { get; set; } = new();
    public List<string> ValidationWarnings { get; set; } = new();
    public bool HasInconsistencies { get; set; }
    public AggregationReasoning? Reasoning { get; set; }
}

/// <summary>
/// Updated percentages for each portfolio
/// </summary>
public class PortfolioPercentages
{
    public decimal? VariableIncomePercentage { get; set; }
    public decimal? FixedIncomePercentage { get; set; }
    public decimal? AlternativeAssetsPercentage { get; set; }
    public decimal? CashPercentage { get; set; }
}

/// <summary>
/// Reasoning for aggregation decisions
/// </summary>
public class AggregationReasoning
{
    public string TotalCalculation { get; set; } = string.Empty;
    public string CurrencyHandling { get; set; } = string.Empty;
    public string PercentageValidation { get; set; } = string.Empty;
    public List<string> AnomaliesDetected { get; set; } = new();
}
