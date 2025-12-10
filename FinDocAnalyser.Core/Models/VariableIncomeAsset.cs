using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FinDocAnalyzer.Core.Models;

/// <summary>
/// Portfolio de Renda Variável (ações, ETFs de equity, ADRs, BDRs, stock options)
/// </summary>
public class VariableIncomePortfolio
{
    public decimal TotalInvested { get; set; }
    public string Currency { get; set; } = "BRL";
    public List<VariableIncomeAsset> Assets { get; set; } = new();
}

/// <summary>
/// Ativo de Renda Variável individual
/// </summary>
public class VariableIncomeAsset
{
    /// <summary>
    /// Ticker/código do ativo (ex: "PETR4", "AAPL", "BOVA11")
    /// </summary>
    public string Ticker { get; set; } = string.Empty;

    /// <summary>
    /// Nome completo do ativo
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Tipo de ativo (ex: "Stock", "ETF", "ADR", "BDR", "Stock Option")
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Quantidade de cotas/ações (aceita fracionário)
    /// </summary>
    public decimal Quantity { get; set; }

    /// <summary>
    /// Preço médio de compra
    /// </summary>
    public decimal AveragePrice { get; set; }

    /// <summary>
    /// Valor atual total da posição
    /// </summary>
    public decimal CurrentValue { get; set; }

    /// <summary>
    /// Retorno absoluto (lucro/prejuízo)
    /// </summary>
    public decimal? Return { get; set; }

    /// <summary>
    /// Retorno percentual
    /// </summary>
    public decimal? ReturnPercentage { get; set; }

    /// <summary>
    /// Dividend yield (ex: "8.62%")
    /// </summary>
    public string Yield { get; set; } = string.Empty;

    /// <summary>
    /// Confiança da extração (0.0 a 1.0)
    /// </summary>
    public decimal Confidence { get; set; }

    /// <summary>
    /// Explicação do nível de confiança
    /// </summary>
    public string ConfidenceReason { get; set; } = string.Empty;
}
