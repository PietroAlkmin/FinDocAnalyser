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

    // Dados extraídos
    public TotalInvested Total { get; set; } = new();
    public AssetClassification Classification { get; set; } = new();
    public StockPortfolio Stocks { get; set; } = new();
    public FixedIncomePortfolio FixedIncome { get; set; } = new();
}
