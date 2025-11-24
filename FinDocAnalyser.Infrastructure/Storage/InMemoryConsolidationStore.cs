using FinDocAnalyzer.Core.Interfaces;
using FinDocAnalyzer.Core.Models;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace FinDocAnalyzer.Infrastructure.Storage;

/// <summary>
/// Armazenamento em memória para consolidações com gerenciamento de capacidade
/// </summary>
public class InMemoryConsolidationStore : IConsolidationStore, IDisposable
{
    private readonly ConcurrentDictionary<Guid, (ConsolidatedPortfolio Data, DateTime ExpiresAt, DateTime LastAccessed)> _store = new();
    private readonly Timer _cleanupTimer;
    private readonly ILogger<InMemoryConsolidationStore>? _logger;
    private readonly int _maxItems;
    private bool _disposed = false;

    public InMemoryConsolidationStore(ILogger<InMemoryConsolidationStore>? logger = null, int maxItems = 1000)
    {
        _logger = logger;
        _maxItems = maxItems;

        // Cleanup a cada 10 minutos
        _cleanupTimer = new Timer(
            callback: _ => CleanupExpiredAsync().GetAwaiter().GetResult(),
            state: null,
            dueTime: TimeSpan.FromMinutes(10),
            period: TimeSpan.FromMinutes(10)
        );
    }

    public Task StoreAsync(ConsolidatedPortfolio consolidation, TimeSpan expiresIn)
    {
        if (consolidation == null)
            throw new ArgumentNullException(nameof(consolidation));

        // Verifica capacidade
        if (_store.Count >= _maxItems)
        {
            EvictOldest();
        }

        var expiresAt = DateTime.UtcNow.Add(expiresIn);
        _store[consolidation.ConsolidationId] = (consolidation, expiresAt, DateTime.UtcNow);

        _logger?.LogDebug("Consolidação {Id} armazenada (expira em {Expiration})",
            consolidation.ConsolidationId, expiresIn);

        return Task.CompletedTask;
    }

    public Task<ConsolidatedPortfolio?> GetAsync(Guid consolidationId)
    {
        if (_store.TryGetValue(consolidationId, out var entry))
        {
            if (entry.ExpiresAt > DateTime.UtcNow)
            {
                // Atualiza último acesso (LRU)
                _store[consolidationId] = (entry.Data, entry.ExpiresAt, DateTime.UtcNow);
                return Task.FromResult<ConsolidatedPortfolio?>(entry.Data);
            }

            // Expirou - remove
            _store.TryRemove(consolidationId, out _);
            _logger?.LogDebug("Consolidação {Id} expirada e removida", consolidationId);
        }

        return Task.FromResult<ConsolidatedPortfolio?>(null);
    }

    public Task<List<ConsolidatedPortfolio>> ListAllAsync()
    {
        var now = DateTime.UtcNow;
        var active = _store
            .Where(kvp => kvp.Value.ExpiresAt > now)
            .Select(kvp => kvp.Value.Data)
            .OrderByDescending(c => c.CreatedAt)
            .ToList();

        return Task.FromResult(active);
    }

    public Task DeleteAsync(Guid consolidationId)
    {
        _store.TryRemove(consolidationId, out _);
        _logger?.LogDebug("Consolidação {Id} removida manualmente", consolidationId);
        return Task.CompletedTask;
    }

    public Task CleanupExpiredAsync()
    {
        var now = DateTime.UtcNow;
        var expiredKeys = _store
            .Where(kvp => kvp.Value.ExpiresAt <= now)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in expiredKeys)
        {
            _store.TryRemove(key, out _);
        }

        if (expiredKeys.Count > 0)
        {
            _logger?.LogInformation("Cleanup: {Count} consolidações expiradas removidas", expiredKeys.Count);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Remove os itens mais antigos quando atinge capacidade máxima (LRU)
    /// </summary>
    private void EvictOldest()
    {
        var toRemove = _store
            .OrderBy(kvp => kvp.Value.LastAccessed)
            .Take(_maxItems / 10) // Remove 10% dos mais antigos
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in toRemove)
        {
            _store.TryRemove(key, out _);
        }

        _logger?.LogWarning("Capacidade máxima atingida. Removidos {Count} itens mais antigos (LRU)", toRemove.Count);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _cleanupTimer?.Dispose();
            _store.Clear();
            _disposed = true;
        }

        GC.SuppressFinalize(this);
    }
}
