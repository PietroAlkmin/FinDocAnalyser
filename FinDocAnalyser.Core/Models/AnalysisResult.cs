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

    // Audit trail (LGPD compliance)
    public AuditInfo Audit { get; set; } = new();

    // Dados extraídos
    public TotalInvested? Total { get; set; } = new();
    public AssetClassification? Classification { get; set; } = new();
    public StockPortfolio? Stocks { get; set; } = new();
    public FixedIncomePortfolio? FixedIncome { get; set; } = new();

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
}
