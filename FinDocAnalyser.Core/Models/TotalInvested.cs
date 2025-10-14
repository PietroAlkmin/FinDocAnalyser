using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FinDocAnalyzer.Core.Models;

public class TotalInvested
{
    public decimal TotalInvestedAmount { get; set; }
    public string Currency { get; set; } = "BRL";
}
