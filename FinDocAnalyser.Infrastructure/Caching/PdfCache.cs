using FinDocAnalyzer.Core.Interfaces;
using FinDocAnalyzer.Core.Models;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Collections.Concurrent;

namespace FinDocAnalyzer.Infrastructure.Caching;

/// <summary>
/// Cache em memória para PDFs usando SHA256 com gerenciamento de capacidade
/// </summary>
public class InMemoryPdfCache : IPdfCache, IDisposable
{
    private readonly ConcurrentDictionary<string, (AnalysisResult Result, DateTime ExpiresAt, DateTime LastAccessed, long Size)> _cache = new();
    private readonly Timer _cleanupTimer;
    private readonly ILogger<InMemoryPdfCache>? _logger;
    private readonly int _maxItems;
    private readonly long _maxMemoryBytes;
    private long _currentMemoryUsage = 0;
    private bool _disposed = false;

    public InMemoryPdfCache(ILogger<InMemoryPdfCache>? logger = null, int maxItems = 1000, long maxMemoryMb = 500)
    {
        _logger = logger;
        _maxItems = maxItems;
        _maxMemoryBytes = maxMemoryMb * 1024 * 1024; // Converte MB para bytes

        // Limpa cache expirado a cada 10 minutos
        _cleanupTimer = new Timer(
            callback: _ => CleanupExpired(),
            state: null,
            dueTime: TimeSpan.FromMinutes(10),
            period: TimeSpan.FromMinutes(10)
        );

        _logger?.LogInformation("PDF Cache inicializado: {MaxItems} itens, {MaxMemory} MB", 
            _maxItems, maxMemoryMb);
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
                // Atualiza último acesso (LRU)
                _cache[fileHash] = (entry.Result, entry.ExpiresAt, DateTime.UtcNow, entry.Size);

                // Marca como vindo do cache
                entry.Result.Metadata.FromCache = true;
                
                _logger?.LogDebug("Cache HIT: {Hash} (economia de {Cost:C})", 
                    fileHash[..8], entry.Result.Metadata.EstimatedCost);

                return Task.FromResult<AnalysisResult?>(entry.Result);
            }

            // Expirou - remove
            if (_cache.TryRemove(fileHash, out var removed))
            {
                Interlocked.Add(ref _currentMemoryUsage, -removed.Size);
            }

            _logger?.LogDebug("Cache MISS: {Hash} (expirado)", fileHash[..8]);
        }
        else
        {
            _logger?.LogDebug("Cache MISS: {Hash}", fileHash[..8]);
        }

        return Task.FromResult<AnalysisResult?>(null);
    }

    public Task SetCachedAnalysisAsync(string fileHash, AnalysisResult result, TimeSpan expiration)
    {
        // Estima tamanho em memória (aproximado)
        var estimatedSize = result.FileSizeBytes + 1024; // PDF + overhead

        // Verifica capacidade (número de itens)
        if (_cache.Count >= _maxItems)
        {
            EvictOldest();
        }

        // Verifica capacidade (memória)
        while (_currentMemoryUsage + estimatedSize > _maxMemoryBytes && _cache.Count > 0)
        {
            EvictOldest();
        }

        var expiresAt = DateTime.UtcNow.Add(expiration);
        _cache[fileHash] = (result, expiresAt, DateTime.UtcNow, estimatedSize);
        Interlocked.Add(ref _currentMemoryUsage, estimatedSize);

        _logger?.LogDebug("Cache SET: {Hash} ({Size} KB, expira em {Expiration})", 
            fileHash[..8], estimatedSize / 1024, expiration);

        return Task.CompletedTask;
    }

    public Task RemoveCachedAnalysisAsync(string fileHash)
    {
        if (_cache.TryRemove(fileHash, out var removed))
        {
            Interlocked.Add(ref _currentMemoryUsage, -removed.Size);
            _logger?.LogDebug("Cache REMOVE: {Hash}", fileHash[..8]);
        }
        return Task.CompletedTask;
    }

    public Task ClearCacheAsync()
    {
        var count = _cache.Count;
        _cache.Clear();
        _currentMemoryUsage = 0;
        _logger?.LogInformation("Cache CLEAR: {Count} itens removidos", count);
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
            if (_cache.TryRemove(key, out var removed))
            {
                Interlocked.Add(ref _currentMemoryUsage, -removed.Size);
            }
        }

        if (expiredKeys.Count > 0)
        {
            _logger?.LogInformation("Cache cleanup: {Count} itens expirados removidos ({Memory} MB liberados)", 
                expiredKeys.Count, 
                (expiredKeys.Sum(k => _cache.TryGetValue(k, out var v) ? v.Size : 0) / 1024.0 / 1024.0));
        }
    }

    /// <summary>
    /// Remove os itens mais antigos (LRU) quando atinge capacidade
    /// </summary>
    private void EvictOldest()
    {
        var toRemove = _cache
            .OrderBy(kvp => kvp.Value.LastAccessed)
            .Take(_maxItems / 10) // Remove 10% dos mais antigos
            .Select(kvp => kvp.Key)
            .ToList();

        long freedMemory = 0;
        foreach (var key in toRemove)
        {
            if (_cache.TryRemove(key, out var removed))
            {
                freedMemory += removed.Size;
                Interlocked.Add(ref _currentMemoryUsage, -removed.Size);
            }
        }

        _logger?.LogWarning("Cache eviction (LRU): {Count} itens removidos, {Memory} MB liberados", 
            toRemove.Count, freedMemory / 1024.0 / 1024.0);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _cleanupTimer?.Dispose();
            _cache.Clear();
            _currentMemoryUsage = 0;
            _disposed = true;
        }

        GC.SuppressFinalize(this);
    }
}

