using FinDocAnalyzer.Core.Interfaces;
using FinDocAnalyzer.Core.Models;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace FinDocAnalyzer.Infrastructure.Storage;

public class InMemoryResultStore : IResultStore, IDisposable
{
    private readonly ConcurrentDictionary<Guid, (AnalysisResult Result, DateTime ExpiresAt)> _store = new();
    private readonly Timer _cleanupTimer;
    private readonly ILogger<InMemoryResultStore>? _logger;
    private bool _disposed = false;

    public InMemoryResultStore(ILogger<InMemoryResultStore>? logger = null)
    {
        _logger = logger;
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
        
        _logger?.LogInformation(
            "[ResultStore] Stored analysis {AnalysisId} - Expires at {ExpiresAt} UTC (in {Minutes} minutes)",
            result.AnalysisId,
            expiresAt,
            expiresIn.TotalMinutes);

        return Task.CompletedTask;
    }

    public Task<AnalysisResult?> GetAsync(Guid analysisId)
    {
        if (_store.TryGetValue(analysisId, out var entry))
        {
            var now = DateTime.UtcNow;
            if (entry.ExpiresAt > now)
            {
                _logger?.LogDebug(
                    "[ResultStore] Retrieved analysis {AnalysisId} - Valid until {ExpiresAt} UTC",
                    analysisId,
                    entry.ExpiresAt);
                return Task.FromResult<AnalysisResult?>(entry.Result);
            }

            _logger?.LogWarning(
                "[ResultStore] Analysis {AnalysisId} EXPIRED - Was valid until {ExpiresAt} UTC, now is {Now} UTC",
                analysisId,
                entry.ExpiresAt,
                now);
            _store.TryRemove(analysisId, out _);
        }
        else
        {
            _logger?.LogWarning("[ResultStore] Analysis {AnalysisId} NOT FOUND in store", analysisId);
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

        if (expiredKeys.Count > 0)
        {
            _logger?.LogInformation(
                "[ResultStore] Cleanup removed {Count} expired analysis results",
                expiredKeys.Count);
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