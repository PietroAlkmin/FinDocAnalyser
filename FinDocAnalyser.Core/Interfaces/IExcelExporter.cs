using FinDocAnalyzer.Core.Models;

namespace FinDocAnalyzer.Core.Interfaces;

/// <summary>
/// Service for exporting analysis results to Excel format
/// </summary>
public interface IExcelExporter
{
    /// <summary>
    /// Exports analysis result to Excel file with multiple sheets
    /// </summary>
    /// <param name="analysis">Complete analysis result</param>
    /// <returns>Excel file as byte array</returns>
    Task<byte[]> ExportToExcelAsync(AnalysisResult analysis);
}
