using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using FinDocAnalyzer.Core.Models;

namespace FinDocAnalyzer.Core.Interfaces;

public interface IResultStore
{
    /// <summary>
    /// Stores analysis result with automatic expiration
    /// </summary>
    /// <param name="result">Complete analysis result</param>
    /// <param name="expiresIn">Time until expiration</param>
    Task StoreAsync(AnalysisResult result, TimeSpan expiresIn);

    /// <summary>
    /// Retrieves analysis result by ID
    /// </summary>
    /// <param name="analysisId">Unique analysis identifier</param>
    /// <returns>Analysis result or null if not found/expired</returns>
    Task<AnalysisResult?> GetAsync(Guid analysisId);

    /// <summary>
    /// Manually deletes a result (optional - results auto-expire)
    /// </summary>
    /// <param name="analysisId">Unique analysis identifier</param>
    Task DeleteAsync(Guid analysisId);
}
