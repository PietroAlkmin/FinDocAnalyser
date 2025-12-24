namespace FinDocAnalyzer.Core.Models;

/// <summary>
/// Represents a financial movement/transaction (purchases, sales, dividends, etc.)
/// </summary>
public class Movement
{
    /// <summary>
    /// Date of the movement
    /// </summary>
    public DateTime Date { get; set; }

    /// <summary>
    /// Type of movement (e.g., "Aplicação", "Resgate", "Compra", "Venda", "Dividendo", "JCP")
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Description or details of the movement
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Asset ticker or name (if applicable)
    /// </summary>
    public string? Asset { get; set; }

    /// <summary>
    /// Gross amount before taxes and fees
    /// </summary>
    public decimal GrossAmount { get; set; }

    /// <summary>
    /// Net amount after taxes and fees
    /// </summary>
    public decimal NetAmount { get; set; }

    /// <summary>
    /// Quantity of units/shares (if applicable)
    /// </summary>
    public decimal? Quantity { get; set; }

    /// <summary>
    /// Unit price or quotation (if applicable)
    /// </summary>
    public decimal? UnitPrice { get; set; }

    /// <summary>
    /// Income tax withheld (if applicable)
    /// </summary>
    public decimal? IncomeTax { get; set; }

    /// <summary>
    /// IOF tax (if applicable)
    /// </summary>
    public decimal? IOF { get; set; }

    /// <summary>
    /// Other fees and costs (brokerage, etc.)
    /// </summary>
    public decimal? Fees { get; set; }

    /// <summary>
    /// Document or operation number
    /// </summary>
    public string? DocumentNumber { get; set; }
}

/// <summary>
/// Container for all movements in the analysis
/// </summary>
public class MovementsAnalysis
{
    public List<Movement> Movements { get; set; } = new();
    public decimal TotalApplications { get; set; }
    public decimal TotalRedemptions { get; set; }
    public decimal TotalDividends { get; set; }
    public decimal TotalTaxes { get; set; }
    public decimal TotalFees { get; set; }
}
