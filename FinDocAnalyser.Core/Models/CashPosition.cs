using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FinDocAnalyzer.Core.Models;

/// <summary>
/// Portfolio de Caixa (saldo disponível, money market, sweep accounts)
/// </summary>
public class CashPortfolio
{
    public decimal TotalBalance { get; set; }
    public string Currency { get; set; } = "BRL";
    public List<CashPosition> Positions { get; set; } = new();
}

/// <summary>
/// Posição individual de caixa
/// </summary>
public class CashPosition
{
    /// <summary>
    /// Nome da conta/posição (ex: "Conta Corrente", "JPM Deposit Sweep", "Money Market Fund")
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Tipo de posição (ex: "Checking Account", "Savings", "Money Market", "Sweep Account")
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Instituição financeira
    /// </summary>
    public string Institution { get; set; } = string.Empty;

    /// <summary>
    /// Saldo atual
    /// </summary>
    public decimal Balance { get; set; }

    /// <summary>
    /// Yield/taxa de juros (se aplicável, ex: "0.25%")
    /// </summary>
    public string Yield { get; set; } = string.Empty;

    /// <summary>
    /// Moeda da posição
    /// </summary>
    public string Currency { get; set; } = "BRL";

    /// <summary>
    /// Se é saldo disponível imediato
    /// </summary>
    public bool IsAvailable { get; set; } = true;

    /// <summary>
    /// Confiança da extração (0.0 a 1.0)
    /// </summary>
    public decimal Confidence { get; set; }

    /// <summary>
    /// Explicação do nível de confiança
    /// </summary>
    public string ConfidenceReason { get; set; } = string.Empty;
}
