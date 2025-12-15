using FinDocAnalyzer.Core.Interfaces;
using FinDocAnalyzer.Core.Models;
using System.Security.Cryptography;
using System.Collections.Concurrent;

namespace FinDocAnalyzer.Infrastructure.Caching;

/// <summary>
/// In-memory cache for PDFs using SHA256
/// </summary>
public class InMemoryPdfCache : IPdfCache
{
    private readonly ConcurrentDictionary<string, (AnalysisResult Result, DateTime ExpiresAt)> _cache = new();
    private readonly Timer _cleanupTimer;

    public InMemoryPdfCache()
    {
        // Clean expired cache every 10 minutes
        _cleanupTimer = new Timer(
            callback: _ => CleanupExpired(),
            state: null,
            dueTime: TimeSpan.FromMinutes(10),
            period: TimeSpan.FromMinutes(10)
        );
    }

    public string ComputeHash(byte[] pdfContent)
    {
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(pdfContent);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    public Task<AnalysisResult?> GetCachedAnalysisAsync(string fileHash)
    {
        if (_cache.TryGetValue(fileHash, out var entry))
        {
            if (entry.ExpiresAt > DateTime.UtcNow)
            {
                // Mark as coming from cache
                entry.Result.Metadata.FromCache = true;
                return Task.FromResult<AnalysisResult?>(entry.Result);
            }

            // Expired - remove
            _cache.TryRemove(fileHash, out _);
        }

        return Task.FromResult<AnalysisResult?>(null);
    }

    public Task SetCachedAnalysisAsync(string fileHash, AnalysisResult result, TimeSpan expiration)
    {
        var expiresAt = DateTime.UtcNow.Add(expiration);
        _cache[fileHash] = (result, expiresAt);
        return Task.CompletedTask;
    }

    public Task RemoveCachedAnalysisAsync(string fileHash)
    {
        _cache.TryRemove(fileHash, out _);
        return Task.CompletedTask;
    }

    public Task ClearCacheAsync()
    {
        _cache.Clear();
        return Task.CompletedTask;
    }

    private void CleanupExpired()
    {
        var now = DateTime.UtcNow;
        var expiredKeys = _cache
            .Where(kvp => kvp.Value.ExpiresAt <= now)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in expiredKeys)
        {
            _cache.TryRemove(key, out _);
        }
    }
}
