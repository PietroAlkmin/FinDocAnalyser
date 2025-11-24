using FinDocAnalyzer.Core.Interfaces;
using FinDocAnalyzer.Core.Models;
using Microsoft.Extensions.Logging;

namespace FinDocAnalyzer.Core.Services;

/// <summary>
/// Serviço de consolidação de múltiplos relatórios financeiros.
/// PRINCÍPIO: AI calcula tudo, código apenas converte moedas e agrega.
/// </summary>
public class ConsolidationService
{
    private readonly IResultStore _resultStore;
    private readonly ICurrencyConverter _currencyConverter;
    private readonly ILogger<ConsolidationService> _logger;
    private readonly Dictionary<Guid, ConsolidatedPortfolio> _consolidations = new();

    public ConsolidationService(
        IResultStore resultStore,
        ICurrencyConverter currencyConverter,
        ILogger<ConsolidationService> logger)
    {
        _resultStore = resultStore;
        _currencyConverter = currencyConverter;
        _logger = logger;
    }

    /// <summary>
    /// Consolida múltiplos relatórios em um único portfolio unificado
    /// </summary>
    public async Task<ConsolidatedPortfolio> ConsolidateAsync(List<Guid> analysisIds, string period = "")
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        _logger.LogInformation("═══════════════════════════════════════════════════════════════");
        _logger.LogInformation("[CONSOLIDATION] Iniciando consolidação de {Count} relatórios", analysisIds.Count);
        _logger.LogInformation("═══════════════════════════════════════════════════════════════");

        // Busca todos os relatórios
        var analyses = new List<AnalysisResult>();
        foreach (var id in analysisIds)
        {
            var analysis = await _resultStore.GetAsync(id);
            if (analysis != null)
            {
                analyses.Add(analysis);
                _logger.LogInformation("[CONSOLIDATION] ✓ {FileName}: {Invested:C} {Currency}", 
                    analysis.FileName, 
                    analysis.Total.TotalInvestedAmount,
                    analysis.Total.Currency);
            }
        }

        if (analyses.Count == 0)
        {
            throw new InvalidOperationException("Nenhum relatório válido encontrado para consolidação");
        }

        // Determina moeda base (moeda com maior volume)
        var baseCurrency = DetermineBaseCurrency(analyses);
        _logger.LogInformation("[CONSOLIDATION] Moeda base determinada: {Currency}", baseCurrency);

        // Agrega dados usando IA + conversão de moeda
        var summary = await AggregateSummaryAsync(analyses, baseCurrency);
        var performance = await AggregatePerformanceAsync(analyses, baseCurrency);
        var liquidity = await AggregateLiquidityAsync(analyses, baseCurrency);
        var diversification = await AggregateDiversificationAsync(analyses, baseCurrency);

        stopwatch.Stop();

        var consolidated = new ConsolidatedPortfolio
        {
            ConsolidationId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(2),
            Period = period,
            Summary = summary,
            Performance = performance,
            Liquidity = liquidity,
            Diversification = diversification,
            Metadata = new ConsolidationMetadata
            {
                ReportsAnalyzed = analyses.Count,
                ReportFileNames = analyses.Select(a => a.FileName).ToList(),
                AnalysisIds = analysisIds,
                ProcessingDuration = stopwatch.Elapsed,
                TotalTokensUsed = analyses.Sum(a => a.Metadata.TokensUsed),
                TotalEstimatedCost = analyses.Sum(a => a.Metadata.EstimatedCost),
                AllReportsFromCache = analyses.All(a => a.Metadata.FromCache),
                OldestReportDate = analyses.Min(a => a.CreatedAt),
                NewestReportDate = analyses.Max(a => a.CreatedAt)
            }
        };

        // Armazena consolidação
        _consolidations[consolidated.ConsolidationId] = consolidated;

        _logger.LogInformation("═══════════════════════════════════════════════════════════════");
        _logger.LogInformation("[CONSOLIDATION] ✓ Concluído: {Invested:C} total, {Return:C} retorno ({ReturnPct:N2}%)",
            summary.TotalInvested, summary.TotalReturn, summary.TotalReturnPercentage);
        _logger.LogInformation("═══════════════════════════════════════════════════════════════");

        return consolidated;
    }

    /// <summary>
    /// Recupera consolidação por ID (alias para compatibilidade)
    /// </summary>
    public Task<ConsolidatedPortfolio?> GetConsolidatedAsync(Guid consolidationId)
    {
        return GetConsolidationAsync(consolidationId);
    }

    /// <summary>
    /// Recupera consolidação por ID
    /// </summary>
    public Task<ConsolidatedPortfolio?> GetConsolidationAsync(Guid consolidationId)
    {
        _consolidations.TryGetValue(consolidationId, out var portfolio);
        return Task.FromResult(portfolio);
    }

    /// <summary>
    /// Lista todas as consolidações
    /// </summary>
    public Task<List<ConsolidatedPortfolio>> ListConsolidationsAsync()
    {
        return Task.FromResult(_consolidations.Values.ToList());
    }

    // ═══════════════════════════════════════════════════════════════════════════════
    // MÉTODOS DE AGREGAÇÃO (usam dados da AI, apenas convertem moeda)
    // ═══════════════════════════════════════════════════════════════════════════════

    private async Task<PortfolioSummary> AggregateSummaryAsync(List<AnalysisResult> analyses, string baseCurrency)
    {
        _logger.LogInformation("───────────────────────────────────────────────────────────────");
        _logger.LogInformation("[SUMMARY] Agregando totais (AI calculou, código converte)");
        _logger.LogInformation("───────────────────────────────────────────────────────────────");

        decimal totalInvested = 0;
        decimal totalReturn = 0;

        foreach (var analysis in analyses)
        {
            var currency = analysis.Total.Currency;
            var invested = analysis.Total.TotalInvestedAmount;
            var returnValue = analysis.Analysis?.TotalReturn ?? 0;

            // Converte se necessário
            if (!currency.Equals(baseCurrency, StringComparison.OrdinalIgnoreCase))
            {
                var rate = await _currencyConverter.GetExchangeRateAsync(currency, baseCurrency);
                invested *= rate;
                returnValue *= rate;
                
                _logger.LogInformation("[SUMMARY] {FileName}: {OrigInvested:C} {Currency} × {Rate:N4} = {Invested:C} {Base}",
                    analysis.FileName, analysis.Total.TotalInvestedAmount, currency, rate, invested, baseCurrency);
            }
            else
            {
                _logger.LogInformation("[SUMMARY] {FileName}: {Invested:C} {Currency} (sem conversão)",
                    analysis.FileName, invested, currency);
            }

            totalInvested += invested;
            totalReturn += returnValue;
        }

        var returnPercentage = totalInvested > 0 ? (totalReturn / totalInvested) * 100 : 0;

        _logger.LogInformation("[SUMMARY] TOTAL: {Invested:C} investido, {Return:C} retorno ({Pct:N2}%)",
            totalInvested, totalReturn, returnPercentage);

        return new PortfolioSummary
        {
            TotalInvested = totalInvested,
            CurrentValue = totalInvested + totalReturn,
            TotalReturn = totalReturn,
            TotalReturnPercentage = returnPercentage,
            Currency = baseCurrency,
            TotalReports = analyses.Count
        };
    }

    private async Task<PortfolioPerformance> AggregatePerformanceAsync(List<AnalysisResult> analyses, string baseCurrency)
    {
        _logger.LogInformation("───────────────────────────────────────────────────────────────");
        _logger.LogInformation("[PERFORMANCE] Agregando desempenho da AI");
        _logger.LogInformation("───────────────────────────────────────────────────────────────");

        string? globalBestAsset = null;
        decimal globalBestReturn = decimal.MinValue;
        string? globalWorstAsset = null;
        decimal globalWorstReturn = decimal.MaxValue;

        var allWarnings = new List<string>();
        var allNotes = new List<string>();
        decimal avgConfidence = 0;

        foreach (var analysis in analyses)
        {
            if (analysis.Analysis == null) continue;

            // Melhor ativo
            if (analysis.Analysis.BestAssetReturn.HasValue && 
                analysis.Analysis.BestAssetReturn.Value > globalBestReturn)
            {
                globalBestReturn = analysis.Analysis.BestAssetReturn.Value;
                globalBestAsset = analysis.Analysis.BestAsset;
            }

            // Pior ativo
            if (analysis.Analysis.WorstAssetReturn.HasValue && 
                analysis.Analysis.WorstAssetReturn.Value < globalWorstReturn)
            {
                globalWorstReturn = analysis.Analysis.WorstAssetReturn.Value;
                globalWorstAsset = analysis.Analysis.WorstAsset;
            }

            // Warnings e notas
            if (analysis.Analysis.Warnings != null)
                allWarnings.AddRange(analysis.Analysis.Warnings);
            
            if (!string.IsNullOrEmpty(analysis.Analysis.Notes))
                allNotes.Add($"[{analysis.FileName}] {analysis.Analysis.Notes}");

            avgConfidence += analysis.Analysis.ConfidenceScore ?? 0;

            _logger.LogInformation("[PERFORMANCE] {FileName}: Melhor={Best} ({BestRet:N2}%), Pior={Worst} ({WorstRet:N2}%), Confiança={Conf:N0}%",
                analysis.FileName,
                analysis.Analysis.BestAsset ?? "N/A",
                analysis.Analysis.BestAssetReturn ?? 0,
                analysis.Analysis.WorstAsset ?? "N/A",
                analysis.Analysis.WorstAssetReturn ?? 0,
                (analysis.Analysis.ConfidenceScore ?? 0) * 100);
        }

        avgConfidence = analyses.Count > 0 ? avgConfidence / analyses.Count : 0;

        _logger.LogInformation("[PERFORMANCE] GLOBAL: Melhor={Best} ({BestRet:N2}%), Pior={Worst} ({WorstRet:N2}%)",
            globalBestAsset ?? "N/A", globalBestReturn, globalWorstAsset ?? "N/A", globalWorstReturn);

        var totalInvested = analyses.Sum(a => a.Total.TotalInvestedAmount);
        var totalReturn = analyses.Sum(a => a.Analysis?.TotalReturn ?? 0);
        var returnPercentage = totalInvested > 0 ? (totalReturn / totalInvested) * 100 : 0;

        return new PortfolioPerformance
        {
            ReturnInPeriod = totalReturn,
            ReturnPercentage = returnPercentage,
            BestPerformer = globalBestAsset ?? "-",
            BestPerformerReturn = globalBestReturn != decimal.MinValue ? globalBestReturn : 0,
            WorstPerformer = globalWorstAsset ?? "-",
            WorstPerformerReturn = globalWorstReturn != decimal.MaxValue ? globalWorstReturn : 0
        };
    }

    private async Task<PortfolioLiquidity> AggregateLiquidityAsync(List<AnalysisResult> analyses, string baseCurrency)
    {
        _logger.LogInformation("───────────────────────────────────────────────────────────────");
        _logger.LogInformation("[LIQUIDITY] Agregando liquidez estimada pela AI");
        _logger.LogInformation("───────────────────────────────────────────────────────────────");

        decimal totalInvested = 0;
        decimal weightedHighPct = 0;
        decimal weightedMediumPct = 0;
        decimal weightedLowPct = 0;

        foreach (var analysis in analyses)
        {
            var currency = analysis.Total.Currency;
            var invested = analysis.Total.TotalInvestedAmount;

            // Converte se necessário
            if (!currency.Equals(baseCurrency, StringComparison.OrdinalIgnoreCase))
            {
                var rate = await _currencyConverter.GetExchangeRateAsync(currency, baseCurrency);
                invested *= rate;
            }

            totalInvested += invested;

            // Usa percentuais estimados pela AI
            var highPct = analysis.Analysis?.HighLiquidityPercentage ?? 0;
            var mediumPct = analysis.Analysis?.MediumLiquidityPercentage ?? 0;
            var lowPct = analysis.Analysis?.LowLiquidityPercentage ?? 0;

            weightedHighPct += highPct * invested;
            weightedMediumPct += mediumPct * invested;
            weightedLowPct += lowPct * invested;

            _logger.LogInformation("[LIQUIDITY] {FileName}: Alta={High:N1}%, Média={Medium:N1}%, Baixa={Low:N1}%",
                analysis.FileName, highPct, mediumPct, lowPct);
        }

        // Média ponderada pelo valor investido
        var finalHighPct = totalInvested > 0 ? weightedHighPct / totalInvested : 0;
        var finalMediumPct = totalInvested > 0 ? weightedMediumPct / totalInvested : 0;
        var finalLowPct = totalInvested > 0 ? weightedLowPct / totalInvested : 0;

        _logger.LogInformation("[LIQUIDITY] CONSOLIDADO: Alta={High:N1}%, Média={Medium:N1}%, Baixa={Low:N1}%",
            finalHighPct, finalMediumPct, finalLowPct);

        return new PortfolioLiquidity
        {
            HighLiquidityPercentage = finalHighPct,
            MediumLiquidityPercentage = finalMediumPct,
            LowLiquidityPercentage = finalLowPct
        };
    }

    private async Task<PortfolioDiversification> AggregateDiversificationAsync(List<AnalysisResult> analyses, string baseCurrency)
    {
        _logger.LogInformation("───────────────────────────────────────────────────────────────");
        _logger.LogInformation("[DIVERSIFICATION] Agregando diversificação da AI");
        _logger.LogInformation("───────────────────────────────────────────────────────────────");

        // Usa HashSet para contar ÚNICOS (não soma duplicados)
        var allIssuers = new HashSet<string>();
        var allAssetTypes = new HashSet<string>();
        
        decimal totalInvested = 0;
        decimal maxAssetValue = 0;
        string? largestAsset = null;

        foreach (var analysis in analyses)
        {
            var currency = analysis.Total.Currency;
            var invested = analysis.Total.TotalInvestedAmount;

            // Converte se necessário
            if (!currency.Equals(baseCurrency, StringComparison.OrdinalIgnoreCase))
            {
                var rate = await _currencyConverter.GetExchangeRateAsync(currency, baseCurrency);
                invested *= rate;
            }

            totalInvested += invested;

            // Coleta emissores únicos de ações
            if (analysis.Stocks?.Stocks != null)
            {
                foreach (var stock in analysis.Stocks.Stocks)
                {
                    allIssuers.Add(stock.Ticker);
                    allAssetTypes.Add("Ações");
                    
                    var stockValue = (stock.CurrentValue ?? stock.TotalInvested ?? 0);
                    if (!currency.Equals(baseCurrency, StringComparison.OrdinalIgnoreCase))
                    {
                        var rate = await _currencyConverter.GetExchangeRateAsync(currency, baseCurrency);
                        stockValue *= rate;
                    }
                    
                    if (stockValue > maxAssetValue)
                    {
                        maxAssetValue = stockValue;
                        largestAsset = stock.Ticker;
                    }
                }
            }

            // Coleta emissores únicos de renda fixa
            if (analysis.FixedIncome?.Assets != null)
            {
                foreach (var asset in analysis.FixedIncome.Assets)
                {
                    allIssuers.Add(asset.Issuer);
                    allAssetTypes.Add(asset.Type); // CDB, LCI, etc são tipos diferentes
                    
                    var assetValue = (asset.CurrentValue ?? asset.InvestedAmount ?? 0);
                    if (!currency.Equals(baseCurrency, StringComparison.OrdinalIgnoreCase))
                    {
                        var rate = await _currencyConverter.GetExchangeRateAsync(currency, baseCurrency);
                        assetValue *= rate;
                    }
                    
                    if (assetValue > maxAssetValue)
                    {
                        maxAssetValue = assetValue;
                        largestAsset = asset.Name;
                    }
                }
            }

            _logger.LogInformation("[DIVERSIFICATION] {FileName}: contribuiu para contagem única de emissores/tipos",
                analysis.FileName);
        }

        // Calcula concentração real do portfolio consolidado
        var concentrationRisk = totalInvested > 0 ? (maxAssetValue / totalInvested) * 100 : 0;

        string score = concentrationRisk < 20 && allAssetTypes.Count >= 3 ? "Boa" :
                      concentrationRisk < 40 ? "Moderada" : "Concentrada";

        _logger.LogInformation("[DIVERSIFICATION] RESULTADO: {Issuers} emissores ÚNICOS, {Types} tipos ÚNICOS, maior ativo: {Concentration:N2}% ({Asset})",
            allIssuers.Count, allAssetTypes.Count, concentrationRisk, largestAsset ?? "N/A");

        return new PortfolioDiversification
        {
            TotalAssetClasses = allAssetTypes.Count,
            TotalIssuers = allIssuers.Count,
            ConcentrationRisk = concentrationRisk,
            LargestAsset = largestAsset ?? "-",
            LargestAssetPercentage = concentrationRisk,
            DiversificationScore = score
        };
    }

    // ═══════════════════════════════════════════════════════════════════════════════
    // MÉTODOS AUXILIARES
    // ═══════════════════════════════════════════════════════════════════════════════

    private string DetermineBaseCurrency(List<AnalysisResult> analyses)
    {
        // Agrupa por moeda e soma valores
        var currencyTotals = analyses
            .GroupBy(a => a.Total.Currency)
            .Select(g => new
            {
                Currency = g.Key,
                Total = g.Sum(a => a.Total.TotalInvestedAmount)
            })
            .OrderByDescending(x => x.Total)
            .ToList();

        // Retorna a moeda com maior volume
        return currencyTotals.FirstOrDefault()?.Currency ?? "BRL";
    }
}
