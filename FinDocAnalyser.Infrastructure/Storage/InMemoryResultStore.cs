using FinDocAnalyzer.Core.Interfaces;
using FinDocAnalyzer.Core.Models;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace FinDocAnalyzer.Infrastructure.Storage;

/// <summary>
/// Armazenamento em memória para resultados de análise com gerenciamento de capacidade
/// </summary>
public class InMemoryResultStore : IResultStore, IDisposable
{
    private readonly ConcurrentDictionary<Guid, (AnalysisResult Result, DateTime ExpiresAt, DateTime LastAccessed)> _store = new();
    private readonly Timer _cleanupTimer;
    private readonly ILogger<InMemoryResultStore>? _logger;
    private readonly int _maxItems;
    private bool _disposed = false;

    public InMemoryResultStore(ILogger<InMemoryResultStore>? logger = null, int maxItems = 10000)
    {
        _logger = logger;
        _maxItems = maxItems;

        _cleanupTimer = new Timer(
            callback: CleanupExpiredItems,
            state: null,
            dueTime: TimeSpan.FromMinutes(5),
            period: TimeSpan.FromMinutes(5)
        );
    }

    public Task StoreAsync(AnalysisResult result, TimeSpan expiresIn)
    {
        if (result == null)
            throw new ArgumentNullException(nameof(result));

        // Verifica capacidade
        if (_store.Count >= _maxItems)
        {
            EvictOldest();
        }

        var expiresAt = DateTime.UtcNow.Add(expiresIn);
        _store[result.AnalysisId] = (result, expiresAt, DateTime.UtcNow);

        _logger?.LogDebug("Análise {Id} armazenada (expira em {Expiration})", 
            result.AnalysisId, expiresIn);

        return Task.CompletedTask;
    }

    public Task<AnalysisResult?> GetAsync(Guid analysisId)
    {
        if (_store.TryGetValue(analysisId, out var entry))
        {
            if (entry.ExpiresAt > DateTime.UtcNow)
            {
                // Atualiza último acesso (LRU)
                _store[analysisId] = (entry.Result, entry.ExpiresAt, DateTime.UtcNow);
                return Task.FromResult<AnalysisResult?>(entry.Result);
            }

            // Expirou - remove
            _store.TryRemove(analysisId, out _);
            _logger?.LogDebug("Análise {Id} expirada e removida", analysisId);
        }

        return Task.FromResult<AnalysisResult?>(null);
    }

    public Task DeleteAsync(Guid analysisId)
    {
        _store.TryRemove(analysisId, out _);
        return Task.CompletedTask;
    }

    private void CleanupExpiredItems(object? state)
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
            _logger?.LogInformation("Cleanup: {Count} análises expiradas removidas", expiredKeys.Count);
        }
    }

    /// <summary>
    /// Remove os itens mais antigos quando atinge capacidade máxima (LRU - Least Recently Used)
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

        _logger?.LogWarning("Capacidade máxima atingida ({MaxItems}). Removidos {Count} itens mais antigos (LRU)", 
            _maxItems, toRemove.Count);
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