using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FinDocAnalyzer.Core.Models;

public class AssetClassification
{
    public decimal TotalInvested { get; set; }
    public string Currency { get; set; } = "BRL";
    public List<AssetClass> Classes { get; set; } = new();
}

public class AssetClass
{
    public string AssetClassName { get; set; } = string.Empty;
    public decimal Invested { get; set; }
    public decimal Percentage { get; set; }
    public decimal Confidence { get; set; }
    public string ConfidenceReason { get; set; } = string.Empty;
}