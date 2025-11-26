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
    private readonly IConsolidationStore _consolidationStore;
    private readonly ICurrencyConverter _currencyConverter;
    private readonly ILogger<ConsolidationService> _logger;

    public ConsolidationService(
        IResultStore resultStore,
        IConsolidationStore consolidationStore,
        ICurrencyConverter currencyConverter,
        ILogger<ConsolidationService> logger)
    {
        _resultStore = resultStore;
        _consolidationStore = consolidationStore;
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
        
        // 🆕 AGREGA ATIVOS DETALHADOS (todos os ativos de todos os PDFs)
        var classification = await AggregateClassificationAsync(analyses, baseCurrency);
        var stocks = await AggregateStocksAsync(analyses, baseCurrency);
        var fixedIncome = await AggregateFixedIncomeAsync(analyses, baseCurrency);

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
            Classification = classification,  // 🆕 Agora populado!
            Stocks = stocks,                   // 🆕 Agora populado!
            FixedIncome = fixedIncome,         // 🆕 Agora populado!
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

        // Armazena consolidação com expiração de 2 horas
        await _consolidationStore.StoreAsync(consolidated, TimeSpan.FromHours(2));

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
        return _consolidationStore.GetAsync(consolidationId);
    }

    /// <summary>
    /// Recupera consolidação por ID
    /// </summary>
    public Task<ConsolidatedPortfolio?> GetConsolidationAsync(Guid consolidationId)
    {
        return _consolidationStore.GetAsync(consolidationId);
    }

    /// <summary>
    /// Lista todas as consolidações
    /// </summary>
    public Task<List<ConsolidatedPortfolio>> ListConsolidationsAsync()
    {
        return _consolidationStore.ListAllAsync();
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

            _logger.LogInformation("[SUMMARY INPUT] {FileName}:", analysis.FileName);
            _logger.LogInformation("  - Total investido ORIGINAL: {Invested:C} {Currency}", invested, currency);
            _logger.LogInformation("  - Retorno calculado pela AI: {Return:C} {Currency}", returnValue, currency);

            // Converte se necessário
            if (!currency.Equals(baseCurrency, StringComparison.OrdinalIgnoreCase))
            {
                var rate = await _currencyConverter.GetExchangeRateAsync(currency, baseCurrency);
                
                var investedBefore = invested;
                var returnBefore = returnValue;
                
                invested *= rate;
                returnValue *= rate;
                
                _logger.LogInformation("  - Taxa de conversão {From}→{To}: {Rate:N4}", currency, baseCurrency, rate);
                _logger.LogInformation("  - APÓS conversão: {Invested:C} {Currency} (foi {Original:C})", 
                    invested, baseCurrency, investedBefore);
                _logger.LogInformation("  - Retorno APÓS conversão: {Return:C} {Currency} (foi {Original:C})", 
                    returnValue, baseCurrency, returnBefore);
            }
            else
            {
                _logger.LogInformation("  - Mesma moeda base, sem conversão");
            }

            totalInvested += invested;
            totalReturn += returnValue;
            
            _logger.LogInformation("  - ACUMULADO até agora: {Total:C} investido, {Return:C} retorno", 
                totalInvested, totalReturn);
        }

        var returnPercentage = totalInvested > 0 ? (totalReturn / totalInvested) * 100 : 0;

        _logger.LogInformation("───────────────────────────────────────────────────────────────");
        _logger.LogInformation("[SUMMARY OUTPUT] TOTAIS FINAIS:");
        _logger.LogInformation("  - Total investido: {Invested:C} {Currency}", totalInvested, baseCurrency);
        _logger.LogInformation("  - Retorno total: {Return:C} {Currency}", totalReturn, baseCurrency);
        _logger.LogInformation("  - Valor atual: {Current:C} {Currency}", totalInvested + totalReturn, baseCurrency);
        _logger.LogInformation("  - Rentabilidade: {Pct:N2}%", returnPercentage);
        _logger.LogInformation("───────────────────────────────────────────────────────────────");

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
    // MÉTODOS DE AGREGAÇÃO DETALHADA (NOVOS - mostram TODOS os ativos)
    // ═══════════════════════════════════════════════════════════════════════════════

    private async Task<ConsolidatedClassification> AggregateClassificationAsync(List<AnalysisResult> analyses, string baseCurrency)
    {
        _logger.LogInformation("───────────────────────────────────────────────────────────────");
        _logger.LogInformation("[CLASSIFICATION] Agregando classificação de ativos de {Count} PDFs", analyses.Count);
        _logger.LogInformation("───────────────────────────────────────────────────────────────");

        var classDict = new Dictionary<string, ConsolidatedAssetClass>();
        decimal totalInvested = 0;

        foreach (var analysis in analyses)
        {
            if (analysis.Classification?.Classes == null) continue;

            var currency = analysis.Total.Currency;
            var rate = await _currencyConverter.GetExchangeRateAsync(currency, baseCurrency);

            _logger.LogInformation("[CLASSIFICATION] Processando {FileName} ({Currency} → {Base}, taxa={Rate:N4})",
                analysis.FileName, currency, baseCurrency, rate);

            foreach (var assetClass in analysis.Classification.Classes)
            {
                var originalInvested = assetClass.Invested;
                var invested = originalInvested * rate;
                totalInvested += invested;

                _logger.LogInformation("  • Classe '{ClassName}': {Original:C} {Currency} × {Rate:N4} = {Converted:C} {Base}",
                    assetClass.AssetClassName, originalInvested, currency, rate, invested, baseCurrency);

                if (!classDict.ContainsKey(assetClass.AssetClassName))
                {
                    classDict[assetClass.AssetClassName] = new ConsolidatedAssetClass
                    {
                        ClassName = assetClass.AssetClassName,
                        Invested = 0,
                        CurrentValue = 0,
                        Return = 0,
                        AssetCount = 0
                    };
                    _logger.LogInformation("    → Nova classe criada: '{ClassName}'", assetClass.AssetClassName);
                }

                var before = classDict[assetClass.AssetClassName].Invested;
                classDict[assetClass.AssetClassName].Invested += invested;
                classDict[assetClass.AssetClassName].CurrentValue += invested; // Será recalculado se houver valor atual
                classDict[assetClass.AssetClassName].AssetCount++;
                var after = classDict[assetClass.AssetClassName].Invested;

                _logger.LogInformation("    → Acumulado '{ClassName}': {Before:C} + {Add:C} = {After:C} {Base}",
                    assetClass.AssetClassName, before, invested, after, baseCurrency);
            }

            _logger.LogInformation("[CLASSIFICATION] ✓ {FileName}: {Classes} classes agregadas",
                analysis.FileName, analysis.Classification.Classes.Count);
        }

        // Calcula percentuais
        foreach (var cls in classDict.Values)
        {
            cls.Percentage = totalInvested > 0 ? (cls.Invested / totalInvested) * 100 : 0;
            cls.Return = cls.CurrentValue - cls.Invested;
            cls.ReturnPercentage = cls.Invested > 0 ? (cls.Return / cls.Invested) * 100 : 0;
        }

        _logger.LogInformation("[CLASSIFICATION] TOTAL: {Classes} classes únicas, {Total:C} {Currency}",
            classDict.Count, totalInvested, baseCurrency);

        return new ConsolidatedClassification
        {
            TotalInvested = totalInvested,
            Currency = baseCurrency,
            Classes = classDict.Values.OrderByDescending(c => c.Invested).ToList()
        };
    }

    private async Task<ConsolidatedStocks> AggregateStocksAsync(List<AnalysisResult> analyses, string baseCurrency)
    {
        _logger.LogInformation("───────────────────────────────────────────────────────────────");
        _logger.LogInformation("[STOCKS] Consolidando ações de {Count} PDFs", analyses.Count);
        _logger.LogInformation("───────────────────────────────────────────────────────────────");

        // Agrupa por ticker (mesmo ticker em diferentes PDFs = mesmo ativo)
        var stockDict = new Dictionary<string, ConsolidatedStock>();
        decimal totalInvested = 0;
        decimal totalCurrentValue = 0;

        foreach (var analysis in analyses)
        {
            if (analysis.Stocks?.Stocks == null) continue;

            var currency = analysis.Total.Currency;
            var rate = await _currencyConverter.GetExchangeRateAsync(currency, baseCurrency);

            _logger.LogInformation("[STOCKS] Processando {FileName} ({Currency} → {Base}, taxa={Rate:N4})",
                analysis.FileName, currency, baseCurrency, rate);

            foreach (var stock in analysis.Stocks.Stocks)
            {
                var ticker = stock.Ticker.ToUpperInvariant();
                var originalInvested = stock.TotalInvested ?? 0;
                var originalCurrent = stock.CurrentValue ?? originalInvested;
                var invested = originalInvested * rate;
                var currentValue = originalCurrent * rate;
                var quantity = stock.Quantity ?? 0;

                _logger.LogInformation("  • {Ticker}: Qtd={Qty}, Investido={OrigInv:C} {Currency} × {Rate:N4} = {Invested:C} {Base}",
                    ticker, quantity, originalInvested, currency, rate, invested, baseCurrency);

                if (!stockDict.ContainsKey(ticker))
                {
                    stockDict[ticker] = new ConsolidatedStock
                    {
                        Ticker = ticker,
                        TotalQuantity = 0,
                        TotalInvested = 0,
                        CurrentValue = 0,
                        AveragePrice = 0
                    };
                    _logger.LogInformation("    → Novo ticker criado: {Ticker}", ticker);
                }

                var beforeQty = stockDict[ticker].TotalQuantity;
                var beforeInv = stockDict[ticker].TotalInvested;
                var beforeCur = stockDict[ticker].CurrentValue;

                stockDict[ticker].TotalQuantity += quantity;
                stockDict[ticker].TotalInvested += invested;
                stockDict[ticker].CurrentValue += currentValue;

                _logger.LogInformation("    → Acumulado {Ticker}: Qtd={BeforeQty}+{AddQty}={AfterQty}, Investido={BeforeInv:C}+{AddInv:C}={AfterInv:C} {Base}",
                    ticker, beforeQty, quantity, stockDict[ticker].TotalQuantity,
                    beforeInv, invested, stockDict[ticker].TotalInvested, baseCurrency);

                totalInvested += invested;
                totalCurrentValue += currentValue;
            }

            _logger.LogInformation("[STOCKS] ✓ {FileName}: {Count} ações agregadas",
                analysis.FileName, analysis.Stocks.Stocks.Count);
        }

        // Calcula preço médio e retornos
        foreach (var stock in stockDict.Values)
        {
            stock.AveragePrice = stock.TotalQuantity > 0 ? stock.TotalInvested / stock.TotalQuantity : 0;
            stock.Return = stock.CurrentValue - stock.TotalInvested;
            stock.ReturnPercentage = stock.TotalInvested > 0 ? (stock.Return / stock.TotalInvested) * 100 : 0;
            stock.PortfolioPercentage = totalInvested > 0 ? (stock.TotalInvested / totalInvested) * 100 : 0;
        }

        var totalReturn = totalCurrentValue - totalInvested;
        var returnPct = totalInvested > 0 ? (totalReturn / totalInvested) * 100 : 0;

        _logger.LogInformation("[STOCKS] TOTAL: {Count} ações únicas, {Invested:C} investido, {Return:C} retorno ({ReturnPct:N2}%)",
            stockDict.Count, totalInvested, totalReturn, returnPct);

        return new ConsolidatedStocks
        {
            TotalInvested = totalInvested,
            CurrentValue = totalCurrentValue,
            TotalReturn = totalReturn,
            ReturnPercentage = returnPct,
            Currency = baseCurrency,
            Stocks = stockDict.Values.OrderByDescending(s => s.TotalInvested).ToList()
        };
    }

    private async Task<ConsolidatedFixedIncome> AggregateFixedIncomeAsync(List<AnalysisResult> analyses, string baseCurrency)
    {
        _logger.LogInformation("───────────────────────────────────────────────────────────────");
        _logger.LogInformation("[FIXED INCOME] Consolidando renda fixa de {Count} PDFs", analyses.Count);
        _logger.LogInformation("───────────────────────────────────────────────────────────────");

        // Agrupa por nome + emissor (CDB XP != CDB BTG)
        var assetDict = new Dictionary<string, ConsolidatedFixedIncomeAsset>();
        decimal totalInvested = 0;
        decimal totalCurrentValue = 0;

        foreach (var analysis in analyses)
        {
            if (analysis.FixedIncome?.Assets == null) continue;

            var currency = analysis.Total.Currency;
            var rate = await _currencyConverter.GetExchangeRateAsync(currency, baseCurrency);

            _logger.LogInformation("[FIXED INCOME] Processando {FileName} ({Currency} → {Base}, taxa={Rate:N4})",
                analysis.FileName, currency, baseCurrency, rate);

            foreach (var asset in analysis.FixedIncome.Assets)
            {
                // Chave única: Tipo + Emissor + Nome (evita duplicatas)
                var key = $"{asset.Type}_{asset.Issuer}_{asset.Name}".ToUpperInvariant();
                var originalInvested = asset.InvestedAmount ?? 0;
                var originalCurrent = asset.CurrentValue ?? originalInvested;
                var invested = originalInvested * rate;
                var currentValue = originalCurrent * rate;

                _logger.LogInformation("  • {Type} {Issuer} '{Name}': Investido={OrigInv:C} {Currency} × {Rate:N4} = {Invested:C} {Base}",
                    asset.Type, asset.Issuer, asset.Name, originalInvested, currency, rate, invested, baseCurrency);

                if (!assetDict.ContainsKey(key))
                {
                    assetDict[key] = new ConsolidatedFixedIncomeAsset
                    {
                        Name = asset.Name,
                        Type = asset.Type,
                        Issuer = asset.Issuer,
                        TotalInvested = 0,
                        CurrentValue = 0,
                        NearestMaturity = asset.MaturityDate
                    };
                    _logger.LogInformation("    → Novo ativo criado: {Key}", key);
                }

                var beforeInv = assetDict[key].TotalInvested;
                var beforeCur = assetDict[key].CurrentValue;

                assetDict[key].TotalInvested += invested;
                assetDict[key].CurrentValue += currentValue;

                _logger.LogInformation("    → Acumulado '{Name}': Investido={BeforeInv:C}+{AddInv:C}={AfterInv:C} {Base}",
                    asset.Name, beforeInv, invested, assetDict[key].TotalInvested, baseCurrency);

                // Mantém o vencimento mais próximo
                if (asset.MaturityDate.HasValue)
                {
                    if (!assetDict[key].NearestMaturity.HasValue ||
                        asset.MaturityDate.Value < assetDict[key].NearestMaturity.Value)
                    {
                        assetDict[key].NearestMaturity = asset.MaturityDate;
                        _logger.LogInformation("    → Vencimento mais próximo atualizado: {Maturity:yyyy-MM-dd}",
                            asset.MaturityDate.Value);
                    }
                }

                totalInvested += invested;
                totalCurrentValue += currentValue;
            }

            _logger.LogInformation("[FIXED INCOME] ✓ {FileName}: {Count} ativos de renda fixa agregados",
                analysis.FileName, analysis.FixedIncome.Assets.Count);
        }

        // Calcula retornos e percentuais
        foreach (var asset in assetDict.Values)
        {
            asset.Return = asset.CurrentValue - asset.TotalInvested;
            asset.ReturnPercentage = asset.TotalInvested > 0 ? (asset.Return / asset.TotalInvested) * 100 : 0;
            asset.PortfolioPercentage = totalInvested > 0 ? (asset.TotalInvested / totalInvested) * 100 : 0;
        }

        var totalReturn = totalCurrentValue - totalInvested;
        var returnPct = totalInvested > 0 ? (totalReturn / totalInvested) * 100 : 0;

        _logger.LogInformation("[FIXED INCOME] TOTAL: {Count} ativos únicos, {Invested:C} investido, {Return:C} retorno ({ReturnPct:N2}%)",
            assetDict.Count, totalInvested, totalReturn, returnPct);

        return new ConsolidatedFixedIncome
        {
            TotalInvested = totalInvested,
            CurrentValue = totalCurrentValue,
            TotalReturn = totalReturn,
            ReturnPercentage = returnPct,
            Currency = baseCurrency,
            Assets = assetDict.Values.OrderByDescending(a => a.TotalInvested).ToList()
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
