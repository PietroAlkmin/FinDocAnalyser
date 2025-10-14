using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using FinDocAnalyzer.Core.Models;

namespace FinDocAnalyzer.Core.Interfaces;

public interface IAnalysisService
{
    /// <summary>
    /// Processes a PDF file and returns analysis ID
    /// </summary>
    /// <param name="pdfContent">PDF file content</param>
    /// <param name="fileName">Original file name</param>
    /// <returns>Unique analysis ID for retrieving results</returns>
    Task<Guid> ProcessPdfAsync(byte[] pdfContent, string fileName);

    /// <summary>
    /// Retrieves complete analysis result
    /// </summary>
    /// <param name="analysisId">Analysis identifier</param>
    /// <returns>Complete analysis or null if not found</returns>
    Task<AnalysisResult?> GetAnalysisAsync(Guid analysisId);
}
