using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using FinDocAnalyzer.Core.Models;

namespace FinDocAnalyzer.Core.Interfaces;

public interface IAiAnalyzer
{
    /// <summary>
    /// Analyzes financial report text and extracts structured data
    /// </summary>
    /// <param name="extractedText">Text extracted from PDF</param>
    /// <returns>Complete analysis result with all financial data</returns>
    Task<AnalysisResult> AnalyzeAsync(string extractedText);
}