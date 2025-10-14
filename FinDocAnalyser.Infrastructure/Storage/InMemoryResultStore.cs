using FinDocAnalyzer.Core.Interfaces;
using FinDocAnalyzer.Core.Models;
using System.Collections.Concurrent;

namespace FinDocAnalyzer.Infrastructure.Storage;

public class InMemoryResultStore : IResultStore, IDisposable
{
    private readonly ConcurrentDictionary<Guid, (AnalysisResult Result, DateTime ExpiresAt)> _store = new();
    private readonly Timer _cleanupTimer;
    private bool _disposed = false;

    public InMemoryResultStore()
    {
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

        var expiresAt = DateTime.UtcNow.Add(expiresIn);
        _store[result.AnalysisId] = (result, expiresAt);

        return Task.CompletedTask;
    }

    public Task<AnalysisResult?> GetAsync(Guid analysisId)
    {
        if (_store.TryGetValue(analysisId, out var entry))
        {
            if (entry.ExpiresAt > DateTime.UtcNow)
            {
                return Task.FromResult<AnalysisResult?>(entry.Result);
            }

            _store.TryRemove(analysisId, out _);
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
        var expiredKeys = new List<Guid>();

        foreach (var kvp in _store)
        {
            if (kvp.Value.ExpiresAt <= now)
            {
                expiredKeys.Add(kvp.Key);
            }
        }

        foreach (var key in expiredKeys)
        {
            _store.TryRemove(key, out _);
        }
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