using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FinDocAnalyzer.Core.Models;

public class StockPortfolio
{
    public decimal TotalInvested { get; set; }
    public string Currency { get; set; } = "BRL";
    public List<StockHolding> Stocks { get; set; } = new();
}

public class StockHolding
{
    public string Ticker { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal AveragePrice { get; set; }
    public decimal TotalInvested { get; set; }
    public decimal? CurrentValue { get; set; }  // ✅ Agora nullable
    public decimal? Return { get; set; }
    public decimal? ReturnPercentage { get; set; }
    public decimal Confidence { get; set; }
    public string ConfidenceReason { get; set; } = string.Empty;
}