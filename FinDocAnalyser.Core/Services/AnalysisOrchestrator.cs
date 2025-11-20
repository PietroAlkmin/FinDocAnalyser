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
    /// Processa um PDF e retorna o ID da análise (com suporte a cache)
    /// </summary>
    public async Task<Guid> ProcessPdfAsync(byte[] pdfContent, string fileName)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            // 1. VALIDAÇÃO: Verifica se é um PDF válido
            if (!_pdfExtractor.IsValidPdf(pdfContent))
            {
                throw new InvalidOperationException("O arquivo enviado não é um PDF válido.");
            }

            // 2. CACHE: Verifica se já processamos este PDF (SHA256)
            string? fileHash = null;
            if (_pdfCache != null)
            {
                fileHash = _pdfCache.ComputeHash(pdfContent);
                var cachedResult = await _pdfCache.GetCachedAnalysisAsync(fileHash);
                
                if (cachedResult != null)
                {
                    // Gera novo ID mas mantém dados
                    cachedResult.AnalysisId = Guid.NewGuid();
                    cachedResult.CreatedAt = DateTime.UtcNow;
                    cachedResult.ExpiresAt = DateTime.UtcNow.AddMinutes(30);

                    // Armazena resultado (novo ID)
                    await _resultStore.StoreAsync(cachedResult, TimeSpan.FromMinutes(30));

                    return cachedResult.AnalysisId;
                }
            }

            // 3. EXTRAÇÃO: Extrai texto do PDF
            var extractedText = await _pdfExtractor.ExtractTextAsync(pdfContent);

            if (string.IsNullOrWhiteSpace(extractedText))
            {
                throw new InvalidOperationException("Não foi possível extrair texto do PDF. O arquivo pode estar vazio ou conter apenas imagens.");
            }

            // 4. ANÁLISE: Envia para IA analisar
            var analysisResult = await _aiAnalyzer.AnalyzeAsync(extractedText);

            stopwatch.Stop();

            // 5. ENRIQUECIMENTO: Adiciona metadados
            analysisResult.FileName = fileName;
            analysisResult.FileSizeBytes = pdfContent.Length;
            analysisResult.FileHash = fileHash ?? string.Empty;
            analysisResult.Audit.ProcessingDuration = stopwatch.Elapsed;

            // 6. CACHE: Salva no cache de PDFs
            if (_pdfCache != null && !string.IsNullOrEmpty(fileHash))
            {
                await _pdfCache.SetCachedAnalysisAsync(fileHash, analysisResult, TimeSpan.FromHours(24));
            }

            // 7. ARMAZENAMENTO: Salva resultado por 30 minutos
            await _resultStore.StoreAsync(analysisResult, TimeSpan.FromMinutes(30));

            // 8. RETORNO: Retorna o ID para o cliente usar nos endpoints
            return analysisResult.AnalysisId;
        }
        catch (InvalidOperationException)
        {
            // Re-throw erros de validação/negócio sem alterar
            throw;
        }
        catch (Exception ex)
        {
            // Encapsula erros inesperados
            throw new InvalidOperationException($"Erro ao processar PDF '{fileName}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Recupera resultado completo de uma análise
    /// </summary>
    public async Task<AnalysisResult?> GetAnalysisAsync(Guid analysisId)
    {
        return await _resultStore.GetAsync(analysisId);
    }

    /// <summary>
    /// Recupera apenas o total investido
    /// </summary>
    public async Task<TotalInvested?> GetTotalAsync(Guid analysisId)
    {
        var result = await _resultStore.GetAsync(analysisId);
        return result?.Total;
    }

    /// <summary>
    /// Recupera apenas a classificação de ativos
    /// </summary>
    public async Task<AssetClassification?> GetClassificationAsync(Guid analysisId)
    {
        var result = await _resultStore.GetAsync(analysisId);
        return result?.Classification;
    }

    /// <summary>
    /// Recupera apenas as ações
    /// </summary>
    public async Task<StockPortfolio?> GetStocksAsync(Guid analysisId)
    {
        var result = await _resultStore.GetAsync(analysisId);
        return result?.Stocks;
    }

    /// <summary>
    /// Recupera apenas renda fixa
    /// </summary>
    public async Task<FixedIncomePortfolio?> GetFixedIncomeAsync(Guid analysisId)
    {
        var result = await _resultStore.GetAsync(analysisId);
        return result?.FixedIncome;
    }
}
