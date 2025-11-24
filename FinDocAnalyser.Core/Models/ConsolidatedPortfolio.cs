using System;
using System.Collections.Generic;

namespace FinDocAnalyzer.Core.Models;

/// <summary>
/// Visão consolidada de toda a carteira do cliente (múltiplos relatórios)
/// </summary>
public class ConsolidatedPortfolio
{
    public Guid ConsolidationId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }

    // Tracking
    public string UserId { get; set; } = string.Empty;
    public string? ClientId { get; set; }

    // Período analisado
    public DateTime? PeriodStart { get; set; }
    public DateTime? PeriodEnd { get; set; }
    public string Period { get; set; } = string.Empty; // "Setembro 2024", "Q3 2024", etc.

    // Resumo Geral
    public PortfolioSummary Summary { get; set; } = new();

    // Rentabilidade
    public PortfolioPerformance Performance { get; set; } = new();

    // Liquidez
    public PortfolioLiquidity Liquidity { get; set; } = new();

    // Diversificação
    public PortfolioDiversification Diversification { get; set; } = new();

    // Composição detalhada
    public ConsolidatedClassification Classification { get; set; } = new();

    // Ativos consolidados
    public ConsolidatedStocks Stocks { get; set; } = new();
    public ConsolidatedFixedIncome FixedIncome { get; set; } = new();

    // Metadados
    public ConsolidationMetadata Metadata { get; set; } = new();
}

/// <summary>
/// Resumo geral da carteira
/// </summary>
public class PortfolioSummary
{
    public decimal TotalInvested { get; set; }
    public decimal CurrentValue { get; set; }
    public decimal TotalReturn { get; set; }
    public decimal TotalReturnPercentage { get; set; }
    public string Currency { get; set; } = "BRL";
    public int TotalAssets { get; set; } // Número total de ativos
    public int TotalReports { get; set; } // Número de relatórios analisados
}

/// <summary>
/// Rentabilidade da carteira no período
/// </summary>
public class PortfolioPerformance
{
    public decimal ReturnInPeriod { get; set; } // Rentabilidade no período (R$)
    public decimal ReturnPercentage { get; set; } // Rentabilidade no período (%)
    public decimal? AnnualizedReturn { get; set; } // Rentabilidade anualizada (%) - null se período desconhecido
    public decimal? Benchmark { get; set; } // Benchmark (CDI, IBOV, etc) - se disponível
    public decimal? Alpha { get; set; } // Alpha vs benchmark
    public string BestPerformer { get; set; } = string.Empty; // Melhor ativo
    public decimal BestPerformerReturn { get; set; }
    public string WorstPerformer { get; set; } = string.Empty; // Pior ativo
    public decimal WorstPerformerReturn { get; set; }
}

/// <summary>
/// Análise de liquidez da carteira
/// </summary>
public class PortfolioLiquidity
{
    public decimal HighLiquidity { get; set; } // Liquidez D+0 (caixa, fundos DI)
    public decimal MediumLiquidity { get; set; } // Liquidez D+1 a D+30
    public decimal LowLiquidity { get; set; } // Liquidez > D+30 ou vencimento futuro
    public decimal HighLiquidityPercentage { get; set; }
    public decimal MediumLiquidityPercentage { get; set; }
    public decimal LowLiquidityPercentage { get; set; }
    public string LiquidityScore { get; set; } = string.Empty; // "Alta", "Média", "Baixa"
}

/// <summary>
/// Diversificação da carteira
/// </summary>
public class PortfolioDiversification
{
    public int TotalAssetClasses { get; set; } // Número de classes de ativos
    public int TotalIssuers { get; set; } // Número de emissores diferentes
    public decimal ConcentrationRisk { get; set; } // % do maior ativo
    public string LargestAsset { get; set; } = string.Empty;
    public decimal LargestAssetPercentage { get; set; }
    public string DiversificationScore { get; set; } = string.Empty; // "Boa", "Moderada", "Concentrada"
}

/// <summary>
/// Classificação consolidada por classe de ativo
/// </summary>
public class ConsolidatedClassification
{
    public decimal TotalInvested { get; set; }
    public string Currency { get; set; } = "BRL";
    public List<ConsolidatedAssetClass> Classes { get; set; } = new();
}

public class ConsolidatedAssetClass
{
    public string ClassName { get; set; } = string.Empty;
    public decimal Invested { get; set; }
    public decimal CurrentValue { get; set; }
    public decimal Return { get; set; }
    public decimal ReturnPercentage { get; set; }
    public decimal Percentage { get; set; }
    public int AssetCount { get; set; } // Número de ativos nesta classe
}

/// <summary>
/// Ações consolidadas
/// </summary>
public class ConsolidatedStocks
{
    public decimal TotalInvested { get; set; }
    public decimal CurrentValue { get; set; }
    public decimal TotalReturn { get; set; }
    public decimal ReturnPercentage { get; set; }
    public string Currency { get; set; } = "BRL";
    public List<ConsolidatedStock> Stocks { get; set; } = new();
}

public class ConsolidatedStock
{
    public string Ticker { get; set; } = string.Empty;
    public int TotalQuantity { get; set; }
    public decimal AveragePrice { get; set; } // Preço médio ponderado
    public decimal TotalInvested { get; set; }
    public decimal CurrentValue { get; set; }
    public decimal Return { get; set; }
    public decimal ReturnPercentage { get; set; }
    public decimal PortfolioPercentage { get; set; }
}

/// <summary>
/// Renda fixa consolidada
/// </summary>
public class ConsolidatedFixedIncome
{
    public decimal TotalInvested { get; set; }
    public decimal CurrentValue { get; set; }
    public decimal TotalReturn { get; set; }
    public decimal ReturnPercentage { get; set; }
    public string Currency { get; set; } = "BRL";
    public List<ConsolidatedFixedIncomeAsset> Assets { get; set; } = new();
}

public class ConsolidatedFixedIncomeAsset
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // CDB, LCI, etc.
    public string Issuer { get; set; } = string.Empty;
    public decimal TotalInvested { get; set; }
    public decimal CurrentValue { get; set; }
    public decimal Return { get; set; }
    public decimal ReturnPercentage { get; set; }
    public decimal PortfolioPercentage { get; set; }
    public DateTime? NearestMaturity { get; set; } // Vencimento mais próximo
}

/// <summary>
/// Metadados da consolidação
/// </summary>
public class ConsolidationMetadata
{
    public int ReportsAnalyzed { get; set; }
    public List<string> ReportFileNames { get; set; } = new();
    public List<Guid> AnalysisIds { get; set; } = new();
    public TimeSpan ProcessingDuration { get; set; }
    public int TotalTokensUsed { get; set; }
    public decimal TotalEstimatedCost { get; set; }
    public bool AllReportsFromCache { get; set; }
    public DateTime OldestReportDate { get; set; }
    public DateTime NewestReportDate { get; set; }
}
