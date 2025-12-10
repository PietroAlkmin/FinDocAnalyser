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
    public string? UserId { get; set; } = string.Empty;
    public string? UserEmail { get; set; }
    public string? ClientId { get; set; }

    // Metadados do arquivo
    public string FileName { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string FileHash { get; set; } = string.Empty; // SHA256 para cache
    public string ExtractedText { get; set; } = string.Empty; // Texto extraído do PDF

    // Audit trail (LGPD compliance)
    public AuditInfo Audit { get; set; } = new();

    // Dados extraídos
    public TotalInvested? Total { get; set; } = new();
    public AssetClassification? Classification { get; set; } = new();
    
    // Portfolios por categoria
    public VariableIncomePortfolio? VariableIncome { get; set; } = new();
    public FixedIncomePortfolio? FixedIncome { get; set; } = new();
    public AlternativeAssetsPortfolio? AlternativeAssets { get; set; } = new();
    public CashPortfolio? Cash { get; set; } = new();
    
    // DEPRECATED: Manter para compatibilidade retroativa
    [Obsolete("Use VariableIncome instead")]
    public StockPortfolio? Stocks { get; set; } = new();

    // Metadados da análise
    public AnalysisMetadata? Metadata { get; set; } = new();
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
    
    // Raw AI response for audit/debugging
    public string? RawAiResponse { get; set; }
    
    // AI reasoning and decision tracking
    public AiReasoning? Reasoning { get; set; }
}

/// <summary>
/// Rastreamento do processo de pensamento da IA
/// </summary>
public class AiReasoning
{
    public string DocumentAnalysis { get; set; } = string.Empty;
    public string CurrencyDetection { get; set; } = string.Empty;
    public string CategoryDecisions { get; set; } = string.Empty;
    public List<string> Uncertainties { get; set; } = new();
    public List<string> Assumptions { get; set; } = new();
    public string DataQualityAssessment { get; set; } = string.Empty;
    public Dictionary<string, string> SpecificDecisions { get; set; } = new();
}
