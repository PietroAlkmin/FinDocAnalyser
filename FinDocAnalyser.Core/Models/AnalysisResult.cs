using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FinDocAnalyzer.Core.Models;

public class AnalysisResult
{
    public Guid AnalysisId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }

    // Tracking de usuário (multi-tenant)
    public string UserId { get; set; } = string.Empty;
    public string? UserEmail { get; set; }
    public string? ClientId { get; set; }

    // Metadados do arquivo
    public string FileName { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string FileHash { get; set; } = string.Empty; // SHA256 para cache

    // Audit trail (LGPD compliance)
    public AuditInfo Audit { get; set; } = new();

    // Dados extraídos
    public TotalInvested Total { get; set; } = new();
    public AssetClassification Classification { get; set; } = new();
    public StockPortfolio Stocks { get; set; } = new();
    public FixedIncomePortfolio FixedIncome { get; set; } = new();

    // Análises calculadas pela AI
    public PortfolioAnalysis? Analysis { get; set; }

    // Metadados da análise
    public AnalysisMetadata Metadata { get; set; } = new();
}

/// <summary>
/// Análises e cálculos feitos pela AI (interpretação inteligente)
/// </summary>
public class PortfolioAnalysis
{
    // Performance calculada pela AI (nullable quando dados insuficientes)
    public decimal? TotalReturn { get; set; } // Retorno total em valor absoluto (moeda do relatório) - null se não calculável
    public decimal? TotalReturnPercentage { get; set; } // Retorno total em % - null se não calculável
    public string? BestAsset { get; set; } // Melhor ativo (ticker ou nome)
    public decimal? BestAssetReturn { get; set; } // Retorno % do melhor ativo
    public string? WorstAsset { get; set; } // Pior ativo
    public decimal? WorstAssetReturn { get; set; } // Retorno % do pior ativo

    // Diversificação analisada pela AI (nullable quando não calculável)
    public int? UniqueIssuers { get; set; } // Número de emissores diferentes
    public int? UniqueAssetTypes { get; set; } // Número de tipos de ativos diferentes
    public decimal? ConcentrationRisk { get; set; } // % do maior ativo individual
    public string? MostConcentratedAsset { get; set; } // Ativo mais concentrado

    // Liquidez analisada pela AI (nullable quando não calculável)
    public decimal? HighLiquidityPercentage { get; set; } // % em ativos de alta liquidez
    public decimal? MediumLiquidityPercentage { get; set; } // % em ativos de média liquidez
    public decimal? LowLiquidityPercentage { get; set; } // % em ativos de baixa liquidez

    // Observações da AI
    public string? Notes { get; set; } // Observações relevantes identificadas pela AI
    public List<string>? Warnings { get; set; } // Alertas (dados incompletos, inconsistências)
    public decimal? ConfidenceScore { get; set; } // Confiança geral da análise (0-1)
}

/// <summary>
/// Informações de auditoria para compliance LGPD
/// </summary>
public class AuditInfo
{
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
    public string? ProcessedBy { get; set; } // Sistema/operador que processou
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public TimeSpan ProcessingDuration { get; set; }
}

/// <summary>
/// Metadados sobre a análise realizada
/// </summary>
public class AnalysisMetadata
{
    public string ReportType { get; set; } = "Generic"; // Bradesco, Offshore, etc
    public int TokensUsed { get; set; }
    public decimal EstimatedCost { get; set; }
    public string AiModel { get; set; } = string.Empty;
    public string AiProvider { get; set; } = "OpenAI";
    public int PagesProcessed { get; set; }
    public bool FromCache { get; set; } = false;
}
