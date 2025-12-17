using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using FinDocAnalyzer.Core.Models;

namespace FinDocAnalyzer.Core.Interfaces;

/// <summary>
/// Aggregator AI that analyzes financial reports independently to extract Total and Classification
/// </summary>
public interface IAggregatorAnalyzer
{
    /// <summary>
    /// Analyzes extracted text independently to calculate total invested and asset classification
    /// </summary>
    /// <param name="extractedText">Text extracted from PDF</param>
    /// <returns>Aggregated result with total and classification, or null if analysis failed</returns>
    Task<AggregatedResult?> AnalyzeAsync(string extractedText);
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
