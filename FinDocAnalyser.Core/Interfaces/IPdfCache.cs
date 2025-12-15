using FinDocAnalyzer.Core.Models;

namespace FinDocAnalyzer.Core.Interfaces;

/// <summary>
/// Cache to prevent reprocessing of identical PDFs
/// </summary>
public interface IPdfCache
{
    /// <summary>
    /// Compute SHA256 hash of PDF content
    /// </summary>
    string ComputeHash(byte[] pdfContent);

    /// <summary>
    /// Check if analysis exists in cache for this hash
    /// </summary>
    Task<AnalysisResult?> GetCachedAnalysisAsync(string fileHash);

    /// <summary>
    /// Store analysis in cache
    /// </summary>
    Task SetCachedAnalysisAsync(string fileHash, AnalysisResult result, TimeSpan expiration);

    /// <summary>
    /// Remove analysis from cache
    /// </summary>
    Task RemoveCachedAnalysisAsync(string fileHash);

    /// <summary>
    /// Clear entire cache
    /// </summary>
    Task ClearCacheAsync();
}
