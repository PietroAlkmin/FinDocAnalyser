using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using FinDocAnalyzer.Core.Interfaces;
using FinDocAnalyzer.Core.Models;

namespace FinDocAnalyzer.Core.Services;

public class AnalysisOrchestrator
{
    private readonly IPdfExtractor _pdfExtractor; //Instanciando as interfaces
    private readonly IAiAnalyzer _aiAnalyzer;
    private readonly IResultStore _resultStore;

    public AnalysisOrchestrator(
        IPdfExtractor pdfExtractor,
        IAiAnalyzer aiAnalyzer,
        IResultStore resultStore)
    {
        _pdfExtractor = pdfExtractor;
        _aiAnalyzer = aiAnalyzer;
        _resultStore = resultStore;
    } // Crei um construtor para receber as interfaces via injeção de dependência

    /// <summary>
    /// Processa um PDF e retorna o ID da análise
    /// </summary>
    public async Task<Guid> ProcessPdfAsync(byte[] pdfContent, string fileName)
    {
        try
        {
            // 1. VALIDAÇÃO: Verifica se é um PDF válido
            if (!_pdfExtractor.IsValidPdf(pdfContent))
            {
                throw new InvalidOperationException("O arquivo enviado não é um PDF válido.");
            }

            // 2. EXTRAÇÃO: Extrai texto do PDF
            var extractedText = await _pdfExtractor.ExtractTextAsync(pdfContent);

            if (string.IsNullOrWhiteSpace(extractedText))
            {
                throw new InvalidOperationException("Não foi possível extrair texto do PDF. O arquivo pode estar vazio ou conter apenas imagens.");
            }

            // 3. ANÁLISE: Envia para IA analisar
            var analysisResult = await _aiAnalyzer.AnalyzeAsync(extractedText);

            // 4. ARMAZENAMENTO: Salva resultado por 30 minutos
            await _resultStore.StoreAsync(analysisResult, TimeSpan.FromMinutes(30));

            // 5. RETORNO: Retorna o ID para o cliente usar nos endpoints
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
