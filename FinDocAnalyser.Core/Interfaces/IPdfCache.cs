using FinDocAnalyzer.Core.Models;

namespace FinDocAnalyzer.Core.Interfaces;

/// <summary>
/// Cache para evitar reprocessamento de PDFs idênticos
/// </summary>
public interface IPdfCache
{
    /// <summary>
    /// Calcula hash SHA256 do conteúdo do PDF
    /// </summary>
    string ComputeHash(byte[] pdfContent);

    /// <summary>
    /// Verifica se existe análise em cache para este hash
    /// </summary>
    Task<AnalysisResult?> GetCachedAnalysisAsync(string fileHash);

    /// <summary>
    /// Armazena análise no cache
    /// </summary>
    Task SetCachedAnalysisAsync(string fileHash, AnalysisResult result, TimeSpan expiration);

    /// <summary>
    /// Remove análise do cache
    /// </summary>
    Task RemoveCachedAnalysisAsync(string fileHash);

    /// <summary>
    /// Limpa todo o cache
    /// </summary>
    Task ClearCacheAsync();
}
