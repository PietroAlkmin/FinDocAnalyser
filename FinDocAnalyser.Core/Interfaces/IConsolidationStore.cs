using FinDocAnalyzer.Core.Models;

namespace FinDocAnalyzer.Core.Interfaces;

/// <summary>
/// Interface para armazenamento de consolidações de carteira
/// </summary>
public interface IConsolidationStore
{
    /// <summary>
    /// Armazena uma consolidação com expiração automática
    /// </summary>
    Task StoreAsync(ConsolidatedPortfolio consolidation, TimeSpan expiresIn);

    /// <summary>
    /// Obtém uma consolidação por ID
    /// </summary>
    Task<ConsolidatedPortfolio?> GetAsync(Guid consolidationId);

    /// <summary>
    /// Lista todas as consolidações não expiradas
    /// </summary>
    Task<List<ConsolidatedPortfolio>> ListAllAsync();

    /// <summary>
    /// Remove uma consolidação manualmente
    /// </summary>
    Task DeleteAsync(Guid consolidationId);

    /// <summary>
    /// Limpa todas as consolidações expiradas
    /// </summary>
    Task CleanupExpiredAsync();
}
