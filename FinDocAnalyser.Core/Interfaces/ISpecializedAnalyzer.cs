using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FinDocAnalyzer.Core.Interfaces;

/// <summary>
/// Specialized AI analyzer for specific asset categories
/// </summary>
/// <typeparam name="TResult">Portfolio type (VariableIncomePortfolio, FixedIncomePortfolio, etc)</typeparam>
public interface ISpecializedAnalyzer<TResult> where TResult : class
{
    /// <summary>
    /// Analyzes extracted text and returns specialized portfolio data
    /// </summary>
    /// <param name="extractedText">Text extracted from PDF</param>
    /// <returns>Portfolio data or null if analysis failed (partial failure)</returns>
    Task<TResult?> AnalyzeAsync(string extractedText);
    
    /// <summary>
    /// Category identifier (e.g., "VariableIncome", "FixedIncome")
    /// </summary>
    string Category { get; }
    
    /// <summary>
    /// Indicates if this analyzer is critical (if fails, whole analysis fails)
    /// Default: false (partial failure is allowed)
    /// </summary>
    bool IsCritical { get; }
}
