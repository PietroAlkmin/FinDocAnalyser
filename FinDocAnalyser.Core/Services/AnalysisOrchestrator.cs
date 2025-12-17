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
    private readonly IAiAnalyzer _aiAnalyzer; // Legacy analyzer (kept for backward compatibility)
    private readonly IResultStore _resultStore;
    private readonly IPdfCache? _pdfCache;
    
    // Specialized analyzers (new chain)
    private readonly ISpecializedAnalyzer<VariableIncomePortfolio>? _variableAnalyzer;
    private readonly ISpecializedAnalyzer<FixedIncomePortfolio>? _fixedAnalyzer;
    private readonly ISpecializedAnalyzer<AlternativeAssetsPortfolio>? _alternativeAnalyzer;
    private readonly ISpecializedAnalyzer<CashPortfolio>? _cashAnalyzer;
    private readonly IAggregatorAnalyzer? _aggregatorAnalyzer;
    
    private readonly bool _useSpecializedChain;

    public AnalysisOrchestrator(
        IPdfExtractor pdfExtractor,
        IAiAnalyzer aiAnalyzer,
        IResultStore resultStore,
        IPdfCache? pdfCache = null,
        ISpecializedAnalyzer<VariableIncomePortfolio>? variableAnalyzer = null,
        ISpecializedAnalyzer<FixedIncomePortfolio>? fixedAnalyzer = null,
        ISpecializedAnalyzer<AlternativeAssetsPortfolio>? alternativeAnalyzer = null,
        ISpecializedAnalyzer<CashPortfolio>? cashAnalyzer = null,
        IAggregatorAnalyzer? aggregatorAnalyzer = null)
    {
        _pdfExtractor = pdfExtractor;
        _aiAnalyzer = aiAnalyzer;
        _resultStore = resultStore;
        _pdfCache = pdfCache;
        _variableAnalyzer = variableAnalyzer;
        _fixedAnalyzer = fixedAnalyzer;
        _alternativeAnalyzer = alternativeAnalyzer;
        _cashAnalyzer = cashAnalyzer;
        _aggregatorAnalyzer = aggregatorAnalyzer;
        
        // Use specialized chain if all analyzers are available
        _useSpecializedChain = variableAnalyzer != null 
            && fixedAnalyzer != null 
            && alternativeAnalyzer != null 
            && cashAnalyzer != null 
            && aggregatorAnalyzer != null;
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

            // 4. ANALYSIS: Choose between specialized chain or legacy analyzer
            AnalysisResult analysisResult;
            
            if (_useSpecializedChain)
            {
                analysisResult = await ProcessWithSpecializedChainAsync(extractedText);
            }
            else
            {
                // Legacy path - single AI analyzer
                analysisResult = await _aiAnalyzer.AnalyzeAsync(extractedText);
            }

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
    /// Process PDF using specialized AI chain (ALL 5 analyzers independently in parallel)
    /// Each AI can fail without affecting the others - no cascading failures
    /// </summary>
    private async Task<AnalysisResult> ProcessWithSpecializedChainAsync(string extractedText)
    {
        // STEP 1: Execute ALL 5 analyzers in PARALLEL (100% independent)
        // Each analyzer reads the PDF directly - no dependencies on other analyzers
        var variableTask = Task.Run(async () =>
        {
            try { return await _variableAnalyzer!.AnalyzeAsync(extractedText); }
            catch { return null; }
        });
        
        var fixedTask = Task.Run(async () =>
        {
            try { return await _fixedAnalyzer!.AnalyzeAsync(extractedText); }
            catch { return null; }
        });
        
        var alternativeTask = Task.Run(async () =>
        {
            try { return await _alternativeAnalyzer!.AnalyzeAsync(extractedText); }
            catch { return null; }
        });
        
        var cashTask = Task.Run(async () =>
        {
            try { return await _cashAnalyzer!.AnalyzeAsync(extractedText); }
            catch { return null; }
        });
        
        var aggregatorTask = Task.Run(async () =>
        {
            try { return await _aggregatorAnalyzer!.AnalyzeAsync(extractedText); }
            catch { return null; }
        });
        
        // Wait for ALL 5 analyzers to complete (or fail)
        await Task.WhenAll(variableTask, fixedTask, alternativeTask, cashTask, aggregatorTask);
        
        // Get results (can be null if analyzer failed)
        var variableIncome = await variableTask;
        var fixedIncome = await fixedTask;
        var alternativeAssets = await alternativeTask;
        var cash = await cashTask;
        var aggregated = await aggregatorTask;
        
        // Track which analyzers succeeded/failed
        var failedAnalyzers = new List<string>();
        if (variableIncome == null) failedAnalyzers.Add("VariableIncome");
        if (fixedIncome == null) failedAnalyzers.Add("FixedIncome");
        if (alternativeAssets == null) failedAnalyzers.Add("AlternativeAssets");
        if (cash == null) failedAnalyzers.Add("Cash");
        if (aggregated == null) failedAnalyzers.Add("Aggregator (Total/Classification)");
        
        // STEP 2: Create final result - each piece can be null independently
        var result = new AnalysisResult
        {
            AnalysisId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(30),
            Total = aggregated?.Total,
            Classification = aggregated?.Classification,
            VariableIncome = variableIncome,
            FixedIncome = fixedIncome,
            AlternativeAssets = alternativeAssets,
            Cash = cash,
            
            // DEPRECATED: Backward compatibility - map VariableIncome to Stocks
#pragma warning disable CS0618 // Type or member is obsolete
            Stocks = variableIncome != null ? new StockPortfolio
            {
                TotalInvested = variableIncome.TotalInvested,
                Currency = variableIncome.Currency,
                Stocks = variableIncome.Assets?.Select(a => new StockHolding
                {
                    Ticker = a.Ticker ?? "",
                    Quantity = (int)a.Quantity,
                    AveragePrice = a.AveragePrice,
                    CurrentValue = a.CurrentValue,
                    Return = a.Return,
                    ReturnPercentage = a.ReturnPercentage,
                    Yield = a.Yield ?? "",
                    Confidence = a.Confidence,
                    ConfidenceReason = a.ConfidenceReason ?? ""
                }).ToList() ?? new List<StockHolding>()
            } : null,
#pragma warning restore CS0618 // Type or member is obsolete
            
            Metadata = new AnalysisMetadata
            {
                AiProvider = "Microsoft.Extensions.AI",
                AiModel = "gpt-4o",
                UsedSpecializedChain = true,
                FailedAnalyzers = failedAnalyzers,
                ValidationWarnings = aggregated?.ValidationWarnings ?? new List<string>(),
                HasInconsistencies = aggregated?.HasInconsistencies ?? false
            },
            
            Audit = new AuditInfo
            {
                ProcessedAt = DateTime.UtcNow
            }
        };
        
        return result;
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
