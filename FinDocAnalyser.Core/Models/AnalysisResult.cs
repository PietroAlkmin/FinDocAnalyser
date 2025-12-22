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

    // User tracking (multi-tenant)
    public string? UserId { get; set; } = string.Empty;
    public string? UserEmail { get; set; }
    public string? ClientId { get; set; }

    // File metadata
    public string FileName { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string FileHash { get; set; } = string.Empty; // SHA256 for cache
    public string ExtractedText { get; set; } = string.Empty; // Extracted text from PDF

    // Audit trail (GDPR compliance)
    public AuditInfo Audit { get; set; } = new();

    // Extracted data
    public TotalInvested? Total { get; set; } = new();
    public AssetClassification? Classification { get; set; } = new();
    
    // Portfolios by category
    public VariableIncomePortfolio? VariableIncome { get; set; } = new();
    public FixedIncomePortfolio? FixedIncome { get; set; } = new();
    public AlternativeAssetsPortfolio? AlternativeAssets { get; set; } = new();
    public CashPortfolio? Cash { get; set; } = new();
    
    // DEPRECATED: Keep for backward compatibility
    [Obsolete("Use VariableIncome instead")]
    public StockPortfolio? Stocks { get; set; } = new();

    // Analysis metadata
    public AnalysisMetadata? Metadata { get; set; } = new();
}

/// <summary>
/// Audit information for GDPR compliance
/// </summary>
public class AuditInfo
{
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
    public string? ProcessedBy { get; set; } // System/operator that processed
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public TimeSpan ProcessingDuration { get; set; }
}

/// <summary>
/// Metadata about the performed analysis
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
    
    // Specialized AI chain metadata
    public bool UsedSpecializedChain { get; set; } = false;
    public List<string> FailedAnalyzers { get; set; } = new();
    public List<string> ValidationWarnings { get; set; } = new();
    public bool HasInconsistencies { get; set; } = false;
    public CrossValidationResult? CrossValidationResult { get; set; }
}

/// <summary>
/// Tracking of AI reasoning process
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

/// <summary>
/// Cross-validation result between Aggregator and Specialized Analyzers
/// </summary>
public class CrossValidationResult
{
    /// <summary>
    /// When the validation was performed
    /// </summary>
    public DateTime PerformedAt { get; set; }
    
    /// <summary>
    /// Total from Aggregator (extracted from summary tables)
    /// </summary>
    public decimal AggregatorTotal { get; set; }
    
    /// <summary>
    /// Sum of all specialized analyzers' contributions
    /// </summary>
    public decimal SpecializedAnalyzersTotal { get; set; }
    
    /// <summary>
    /// Absolute difference between the two totals
    /// </summary>
    public decimal AbsoluteDifference { get; set; }
    
    /// <summary>
    /// Percentage difference
    /// </summary>
    public decimal PercentageDifference { get; set; }
    
    /// <summary>
    /// Individual analyzer totals for detailed breakdown
    /// </summary>
    public decimal VariableIncomeTotal { get; set; }
    public decimal FixedIncomeTotal { get; set; }
    public decimal AlternativeAssetsTotal { get; set; }
    public decimal CashTotal { get; set; }
    
    /// <summary>
    /// Validation warnings
    /// </summary>
    public List<string> Warnings { get; set; } = new();
    
    /// <summary>
    /// Whether inconsistencies were detected beyond tolerance
    /// </summary>
    public bool HasInconsistencies { get; set; }
}
