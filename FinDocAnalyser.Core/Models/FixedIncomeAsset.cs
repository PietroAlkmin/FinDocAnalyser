using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FinDocAnalyzer.Core.Models;

public class FixedIncomePortfolio
{
    public decimal TotalInvested { get; set; }
    public string Currency { get; set; } = "BRL";
    public List<FixedIncomeAsset> Assets { get; set; } = new();
}

public class FixedIncomeAsset
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;  // LCI, CDB, Debênture, etc.
    public string Issuer { get; set; } = string.Empty;
    public decimal InvestedAmount { get; set; }
    public decimal? CurrentValue { get; set; }  // ← Agora nullable
    public decimal? Return { get; set; }
    public decimal? ReturnPercentage { get; set; }
    public string Rate { get; set; } = string.Empty;  // "96% CDI", "IPCA +3.6%"
    public DateTime? MaturityDate { get; set; }  // Nullable
    public DateTime? ApplicationDate { get; set; }  // Nullable
    public decimal Confidence { get; set; }
    public string ConfidenceReason { get; set; } = string.Empty;
}
