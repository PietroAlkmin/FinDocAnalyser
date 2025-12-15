using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

using FinDocAnalyzer.Core.Interfaces;
using FinDocAnalyzer.Core.Models;

namespace FinDocAnalyzer.Core.Services;

public class AnalysisOrchestrator
{
    private readonly IPdfExtractor _pdfExtractor;
    private readonly IAiAnalyzer _aiAnalyzer;
    private readonly IResultStore _resultStore;
    private readonly IPdfCache? _pdfCache;

    public AnalysisOrchestrator(
        IPdfExtractor pdfExtractor,
        IAiAnalyzer aiAnalyzer,
        IResultStore resultStore,
        IPdfCache? pdfCache = null)
    {
        _pdfExtractor = pdfExtractor;
        _aiAnalyzer = aiAnalyzer;
        _resultStore = resultStore;
        _pdfCache = pdfCache;
    }

    /// <summary>
    /// Process a PDF and return the analysis ID (with cache support)
    /// </summary>
    public async Task<Guid> ProcessPdfAsync(byte[] pdfContent, string fileName)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            // 1. VALIDATION: Check if it's a valid PDF
            if (!_pdfExtractor.IsValidPdf(pdfContent))
            {
                throw new InvalidOperationException("The uploaded file is not a valid PDF.");
            }

            // 2. CACHE: Check if we've already processed this PDF (SHA256)
            string? fileHash = null;
            if (_pdfCache != null)
            {
                fileHash = _pdfCache.ComputeHash(pdfContent);
                var cachedResult = await _pdfCache.GetCachedAnalysisAsync(fileHash);
                
                if (cachedResult != null)
                {
                    // Generate new ID but keep data
                    cachedResult.AnalysisId = Guid.NewGuid();
                    cachedResult.CreatedAt = DateTime.UtcNow;
                    cachedResult.ExpiresAt = DateTime.UtcNow.AddMinutes(30);

                    // Store result (new ID)
                    await _resultStore.StoreAsync(cachedResult, TimeSpan.FromMinutes(30));

                    return cachedResult.AnalysisId;
                }
            }

            // 3. EXTRACTION: Extract text from PDF
            var extractedText = await _pdfExtractor.ExtractTextAsync(pdfContent);

            if (string.IsNullOrWhiteSpace(extractedText))
            {
                throw new InvalidOperationException("Could not extract text from PDF. The file may be empty or contain only images.");
            }

            // 4. ANALYSIS: Send to AI for analysis
            var analysisResult = await _aiAnalyzer.AnalyzeAsync(extractedText);

            stopwatch.Stop();

            // 5. ENRICHMENT: Add metadata
            analysisResult.FileName = fileName;
            analysisResult.FileSizeBytes = pdfContent.Length;
            analysisResult.FileHash = fileHash ?? string.Empty;
            analysisResult.ExtractedText = extractedText;
            analysisResult.Audit.ProcessingDuration = stopwatch.Elapsed;

            // 6. CACHE: Save to PDF cache
            if (_pdfCache != null && !string.IsNullOrEmpty(fileHash))
            {
                await _pdfCache.SetCachedAnalysisAsync(fileHash, analysisResult, TimeSpan.FromHours(24));
            }

            // 7. STORAGE: Save result for 30 minutes
            await _resultStore.StoreAsync(analysisResult, TimeSpan.FromMinutes(30));

            // 8. RETURN: Return the ID for client to use in endpoints
            return analysisResult.AnalysisId;
        }
        catch (InvalidOperationException)
        {
            // Re-throw validation/business errors without modifying
            throw;
        }
        catch (Exception ex)
        {
            // Wrap unexpected errors
            throw new InvalidOperationException($"Error processing PDF '{fileName}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Retrieve complete analysis result
    /// </summary>
    public async Task<AnalysisResult?> GetAnalysisAsync(Guid analysisId)
    {
        return await _resultStore.GetAsync(analysisId);
    }

    /// <summary>
    /// Retrieve only total invested amount
    /// </summary>
    public async Task<TotalInvested?> GetTotalAsync(Guid analysisId)
    {
        var result = await _resultStore.GetAsync(analysisId);
        return result?.Total;
    }

    /// <summary>
    /// Retrieve only asset classification
    /// </summary>
    public async Task<AssetClassification?> GetClassificationAsync(Guid analysisId)
    {
        var result = await _resultStore.GetAsync(analysisId);
        return result?.Classification;
    }

    /// <summary>
    /// Retrieve only stocks portfolio
    /// </summary>
    public async Task<StockPortfolio?> GetStocksAsync(Guid analysisId)
    {
        var result = await _resultStore.GetAsync(analysisId);
#pragma warning disable CS0618 // Type or member is obsolete
        return result?.Stocks;
#pragma warning restore CS0618 // Type or member is obsolete
    }

    /// <summary>
    /// Retrieve only fixed income portfolio
    /// </summary>
    public async Task<FixedIncomePortfolio?> GetFixedIncomeAsync(Guid analysisId)
    {
        var result = await _resultStore.GetAsync(analysisId);
        return result?.FixedIncome;
    }

    /// <summary>
    /// Retrieve only variable income portfolio
    /// </summary>
    public async Task<VariableIncomePortfolio?> GetVariableIncomeAsync(Guid analysisId)
    {
        var result = await _resultStore.GetAsync(analysisId);
        return result?.VariableIncome;
    }

    /// <summary>
    /// Retrieve only alternative assets portfolio
    /// </summary>
    public async Task<AlternativeAssetsPortfolio?> GetAlternativeAssetsAsync(Guid analysisId)
    {
        var result = await _resultStore.GetAsync(analysisId);
        return result?.AlternativeAssets;
    }

    /// <summary>
    /// Retrieve only cash positions
    /// </summary>
    public async Task<CashPortfolio?> GetCashAsync(Guid analysisId)
    {
        var result = await _resultStore.GetAsync(analysisId);
        return result?.Cash;
    }

    /// <summary>
    /// Retrieve only extracted text from PDF
    /// </summary>
    public async Task<string?> GetExtractedTextAsync(Guid analysisId)
    {
        var result = await _resultStore.GetAsync(analysisId);
        return result?.ExtractedText;
    }
}
