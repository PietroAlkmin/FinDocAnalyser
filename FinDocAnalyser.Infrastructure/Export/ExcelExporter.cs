using ClosedXML.Excel;
using FinDocAnalyzer.Core.Interfaces;
using FinDocAnalyzer.Core.Models;

namespace FinDocAnalyzer.Infrastructure.Export;

/// <summary>
/// Excel exporter using ClosedXML
/// Creates multi-sheet workbooks with analysis data
/// </summary>
public class ExcelExporter : IExcelExporter
{
    public async Task<byte[]> ExportToExcelAsync(AnalysisResult analysis)
    {
        return await Task.Run(() =>
        {
            using var workbook = new XLWorkbook();

            // Create sheets
            CreateSummarySheet(workbook, analysis);
            
            if (analysis.VariableIncome?.Assets?.Any() == true)
                CreateVariableIncomeSheet(workbook, analysis.VariableIncome);
            
            if (analysis.FixedIncome?.Assets?.Any() == true)
                CreateFixedIncomeSheet(workbook, analysis.FixedIncome);
            
            if (analysis.AlternativeAssets?.Assets?.Any() == true)
                CreateAlternativeAssetsSheet(workbook, analysis.AlternativeAssets);
            
            if (analysis.Cash?.Positions?.Any() == true)
                CreateCashSheet(workbook, analysis.Cash);
            
            if (analysis.Movements?.Movements?.Any() == true)
                CreateMovementsSheet(workbook, analysis.Movements);

            // Save to memory stream
            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        });
    }

    private void CreateSummarySheet(IXLWorkbook workbook, AnalysisResult analysis)
    {
        var ws = workbook.Worksheets.Add("Resumo");

        // Header
        ws.Cell("A1").Value = "RESUMO DA ANÁLISE";
        ws.Cell("A1").Style.Font.Bold = true;
        ws.Cell("A1").Style.Font.FontSize = 16;

        int row = 3;

        // File info
        ws.Cell(row, 1).Value = "Arquivo:";
        ws.Cell(row, 2).Value = analysis.FileName;
        row++;

        ws.Cell(row, 1).Value = "Data da Análise:";
        ws.Cell(row, 2).Value = analysis.CreatedAt;
        ws.Cell(row, 2).Style.DateFormat.Format = "dd/MM/yyyy HH:mm";
        row += 2;

        // Total
        if (analysis.Total != null)
        {
            ws.Cell(row, 1).Value = "Total Investido:";
            ws.Cell(row, 2).Value = analysis.Total.TotalInvestedAmount;
            ws.Cell(row, 2).Style.NumberFormat.Format = "R$ #,##0.00";
            ws.Cell(row, 1).Style.Font.Bold = true;
            ws.Cell(row, 2).Style.Font.Bold = true;
            row += 2;
        }

        // Classification
        if (analysis.Classification?.Classes?.Any() == true)
        {
            ws.Cell(row, 1).Value = "CLASSIFICAÇÃO";
            ws.Cell(row, 1).Style.Font.Bold = true;
            row++;

            ws.Cell(row, 1).Value = "Classe";
            ws.Cell(row, 2).Value = "Investido";
            ws.Cell(row, 3).Value = "Percentual";
            ws.Range(row, 1, row, 3).Style.Font.Bold = true;
            ws.Range(row, 1, row, 3).Style.Fill.BackgroundColor = XLColor.LightGray;
            row++;

            foreach (var @class in analysis.Classification.Classes)
            {
                ws.Cell(row, 1).Value = @class.AssetClassName;
                ws.Cell(row, 2).Value = @class.Invested;
                ws.Cell(row, 2).Style.NumberFormat.Format = "R$ #,##0.00";
                ws.Cell(row, 3).Value = @class.Percentage / 100;
                ws.Cell(row, 3).Style.NumberFormat.Format = "0.00%";
                row++;
            }
        }

        // Auto-fit columns
        ws.Columns().AdjustToContents();
    }

    private void CreateVariableIncomeSheet(IXLWorkbook workbook, VariableIncomePortfolio portfolio)
    {
        var ws = workbook.Worksheets.Add("Renda Variável");

        // Header row
        ws.Cell(1, 1).Value = "Ticker";
        ws.Cell(1, 2).Value = "Nome";
        ws.Cell(1, 3).Value = "Tipo";
        ws.Cell(1, 4).Value = "Quantidade";
        ws.Cell(1, 5).Value = "Preço Médio";
        ws.Cell(1, 6).Value = "Custo Total";
        ws.Cell(1, 7).Value = "Valor Atual";
        ws.Cell(1, 8).Value = "Rentabilidade %";
        ws.Cell(1, 9).Value = "Lucro/Prejuízo";

        ws.Range(1, 1, 1, 9).Style.Font.Bold = true;
        ws.Range(1, 1, 1, 9).Style.Fill.BackgroundColor = XLColor.LightBlue;

        int row = 2;
        foreach (var asset in portfolio.Assets)
        {
            // Calculate missing values when AI doesn't extract them
            var unitPrice = asset.UnitPrice ?? (asset.Quantity > 0 ? asset.CurrentValue / asset.Quantity : 0);
            var totalCost = unitPrice * asset.Quantity;
            var profitLoss = asset.Return ?? (asset.CurrentValue - totalCost);
            var returnPct = asset.ReturnPercentage ?? (totalCost > 0 ? (profitLoss / totalCost) * 100 : 0);
            
            ws.Cell(row, 1).Value = asset.Ticker;
            ws.Cell(row, 2).Value = asset.Name;
            ws.Cell(row, 3).Value = asset.Type;
            ws.Cell(row, 4).Value = asset.Quantity;
            ws.Cell(row, 5).Value = unitPrice;
            ws.Cell(row, 5).Style.NumberFormat.Format = "R$ #,##0.00";
            ws.Cell(row, 6).Value = totalCost;
            ws.Cell(row, 6).Style.NumberFormat.Format = "R$ #,##0.00";
            ws.Cell(row, 7).Value = asset.CurrentValue;
            ws.Cell(row, 7).Style.NumberFormat.Format = "R$ #,##0.00";
            ws.Cell(row, 8).Value = returnPct / 100;
            ws.Cell(row, 8).Style.NumberFormat.Format = "0.00%";
            ws.Cell(row, 9).Value = profitLoss;
            ws.Cell(row, 9).Style.NumberFormat.Format = "R$ #,##0.00";
            
            // Color code profit/loss
            if (profitLoss > 0)
                ws.Cell(row, 9).Style.Font.FontColor = XLColor.Green;
            else if (profitLoss < 0)
                ws.Cell(row, 9).Style.Font.FontColor = XLColor.Red;

            row++;
        }

        // Totals row
        ws.Cell(row, 1).Value = "TOTAL";
        ws.Cell(row, 1).Style.Font.Bold = true;
        ws.Cell(row, 6).Value = portfolio.TotalContribution;
        ws.Cell(row, 6).Style.NumberFormat.Format = "R$ #,##0.00";
        ws.Cell(row, 6).Style.Font.Bold = true;

        ws.Columns().AdjustToContents();
    }

    private void CreateFixedIncomeSheet(IXLWorkbook workbook, FixedIncomePortfolio portfolio)
    {
        var ws = workbook.Worksheets.Add("Renda Fixa");

        // Header
        ws.Cell(1, 1).Value = "Nome";
        ws.Cell(1, 2).Value = "Tipo";
        ws.Cell(1, 3).Value = "Taxa";
        ws.Cell(1, 4).Value = "Vencimento";
        ws.Cell(1, 5).Value = "Valor Investido";
        ws.Cell(1, 6).Value = "Valor Atual";
        ws.Cell(1, 7).Value = "Rentabilidade %";

        ws.Range(1, 1, 1, 7).Style.Font.Bold = true;
        ws.Range(1, 1, 1, 7).Style.Fill.BackgroundColor = XLColor.LightGreen;

        int row = 2;
        foreach (var asset in portfolio.Assets)
        {
            ws.Cell(row, 1).Value = asset.Name;
            ws.Cell(row, 2).Value = asset.Type;
            ws.Cell(row, 3).Value = asset.Rate;
            ws.Cell(row, 4).Value = "-"; // No maturity date in current model
            ws.Cell(row, 5).Value = asset.InvestedAmount;
            ws.Cell(row, 5).Style.NumberFormat.Format = "R$ #,##0.00";
            ws.Cell(row, 6).Value = asset.CurrentValue;
            ws.Cell(row, 6).Style.NumberFormat.Format = "R$ #,##0.00";
            ws.Cell(row, 7).Value = (asset.ReturnPercentage ?? 0) / 100;
            ws.Cell(row, 7).Style.NumberFormat.Format = "0.00%";
            row++;
        }

        // Total
        ws.Cell(row, 1).Value = "TOTAL";
        ws.Cell(row, 1).Style.Font.Bold = true;
        ws.Cell(row, 6).Value = portfolio.TotalContribution;
        ws.Cell(row, 6).Style.NumberFormat.Format = "R$ #,##0.00";
        ws.Cell(row, 6).Style.Font.Bold = true;

        ws.Columns().AdjustToContents();
    }

    private void CreateAlternativeAssetsSheet(IXLWorkbook workbook, AlternativeAssetsPortfolio portfolio)
    {
        var ws = workbook.Worksheets.Add("Ativos Alternativos");

        // Header
        ws.Cell(1, 1).Value = "Nome";
        ws.Cell(1, 2).Value = "Tipo";
        ws.Cell(1, 3).Value = "Valor Investido";
        ws.Cell(1, 4).Value = "Valor Atual";
        ws.Cell(1, 5).Value = "Rentabilidade %";

        ws.Range(1, 1, 1, 5).Style.Font.Bold = true;
        ws.Range(1, 1, 1, 5).Style.Fill.BackgroundColor = XLColor.LightYellow;

        int row = 2;
        foreach (var asset in portfolio.Assets)
        {
            ws.Cell(row, 1).Value = asset.Name;
            ws.Cell(row, 2).Value = asset.Type;
            ws.Cell(row, 3).Value = asset.InvestedAmount;
            ws.Cell(row, 3).Style.NumberFormat.Format = "R$ #,##0.00";
            ws.Cell(row, 4).Value = asset.CurrentValue;
            ws.Cell(row, 4).Style.NumberFormat.Format = "R$ #,##0.00";
            ws.Cell(row, 5).Value = asset.ReturnPercentage / 100;
            ws.Cell(row, 5).Style.NumberFormat.Format = "0.00%";
            row++;
        }

        // Total
        ws.Cell(row, 1).Value = "TOTAL";
        ws.Cell(row, 1).Style.Font.Bold = true;
        ws.Cell(row, 4).Value = portfolio.TotalContribution;
        ws.Cell(row, 4).Style.NumberFormat.Format = "R$ #,##0.00";
        ws.Cell(row, 4).Style.Font.Bold = true;

        ws.Columns().AdjustToContents();
    }

    private void CreateCashSheet(IXLWorkbook workbook, CashPortfolio portfolio)
    {
        var ws = workbook.Worksheets.Add("Caixa");

        // Header
        ws.Cell(1, 1).Value = "Nome";
        ws.Cell(1, 2).Value = "Instituição";
        ws.Cell(1, 3).Value = "Saldo";

        ws.Range(1, 1, 1, 3).Style.Font.Bold = true;
        ws.Range(1, 1, 1, 3).Style.Fill.BackgroundColor = XLColor.LightCyan;

        int row = 2;
        foreach (var position in portfolio.Positions)
        {
            ws.Cell(row, 1).Value = position.Name;
            ws.Cell(row, 2).Value = position.Institution;
            ws.Cell(row, 3).Value = position.Balance;
            ws.Cell(row, 3).Style.NumberFormat.Format = "R$ #,##0.00";
            row++;
        }

        // Total
        ws.Cell(row, 1).Value = "TOTAL";
        ws.Cell(row, 1).Style.Font.Bold = true;
        ws.Cell(row, 3).Value = portfolio.TotalContribution;
        ws.Cell(row, 3).Style.NumberFormat.Format = "R$ #,##0.00";
        ws.Cell(row, 3).Style.Font.Bold = true;

        ws.Columns().AdjustToContents();
    }

    private void CreateMovementsSheet(IXLWorkbook workbook, MovementsAnalysis movements)
    {
        var ws = workbook.Worksheets.Add("Movimentação");

        // Header
        ws.Cell(1, 1).Value = "Data";
        ws.Cell(1, 2).Value = "Tipo";
        ws.Cell(1, 3).Value = "Descrição";
        ws.Cell(1, 4).Value = "Ativo";
        ws.Cell(1, 5).Value = "Valor Bruto";
        ws.Cell(1, 6).Value = "Valor Líquido";
        ws.Cell(1, 7).Value = "Quantidade";
        ws.Cell(1, 8).Value = "Preço Unitário";
        ws.Cell(1, 9).Value = "IR";
        ws.Cell(1, 10).Value = "IOF";
        ws.Cell(1, 11).Value = "Taxas";
        ws.Cell(1, 12).Value = "Nº Documento";

        ws.Range(1, 1, 1, 12).Style.Font.Bold = true;
        ws.Range(1, 1, 1, 12).Style.Fill.BackgroundColor = XLColor.LightGray;

        int row = 2;
        foreach (var movement in movements.Movements.OrderByDescending(m => m.Date))
        {
            ws.Cell(row, 1).Value = movement.Date;
            ws.Cell(row, 1).Style.DateFormat.Format = "dd/MM/yyyy";
            ws.Cell(row, 2).Value = movement.Type;
            ws.Cell(row, 3).Value = movement.Description;
            ws.Cell(row, 4).Value = movement.Asset ?? "";
            ws.Cell(row, 5).Value = movement.GrossAmount;
            ws.Cell(row, 5).Style.NumberFormat.Format = "R$ #,##0.00";
            ws.Cell(row, 6).Value = movement.NetAmount;
            ws.Cell(row, 6).Style.NumberFormat.Format = "R$ #,##0.00";
            ws.Cell(row, 7).Value = movement.Quantity ?? 0;
            ws.Cell(row, 8).Value = movement.UnitPrice ?? 0;
            ws.Cell(row, 8).Style.NumberFormat.Format = "R$ #,##0.00";
            ws.Cell(row, 9).Value = movement.IncomeTax ?? 0;
            ws.Cell(row, 9).Style.NumberFormat.Format = "R$ #,##0.00";
            ws.Cell(row, 10).Value = movement.IOF ?? 0;
            ws.Cell(row, 10).Style.NumberFormat.Format = "R$ #,##0.00";
            ws.Cell(row, 11).Value = movement.Fees ?? 0;
            ws.Cell(row, 11).Style.NumberFormat.Format = "R$ #,##0.00";
            ws.Cell(row, 12).Value = movement.DocumentNumber ?? "";
            row++;
        }

        // Summary
        row += 2;
        ws.Cell(row, 1).Value = "RESUMO";
        ws.Cell(row, 1).Style.Font.Bold = true;
        row++;

        ws.Cell(row, 1).Value = "Total Aplicações:";
        ws.Cell(row, 2).Value = movements.TotalApplications;
        ws.Cell(row, 2).Style.NumberFormat.Format = "R$ #,##0.00";
        row++;

        ws.Cell(row, 1).Value = "Total Resgates:";
        ws.Cell(row, 2).Value = movements.TotalRedemptions;
        ws.Cell(row, 2).Style.NumberFormat.Format = "R$ #,##0.00";
        row++;

        ws.Cell(row, 1).Value = "Total Proventos:";
        ws.Cell(row, 2).Value = movements.TotalDividends;
        ws.Cell(row, 2).Style.NumberFormat.Format = "R$ #,##0.00";
        row++;

        ws.Cell(row, 1).Value = "Total Impostos:";
        ws.Cell(row, 2).Value = movements.TotalTaxes;
        ws.Cell(row, 2).Style.NumberFormat.Format = "R$ #,##0.00";
        row++;

        ws.Cell(row, 1).Value = "Total Taxas:";
        ws.Cell(row, 2).Value = movements.TotalFees;
        ws.Cell(row, 2).Style.NumberFormat.Format = "R$ #,##0.00";

        ws.Columns().AdjustToContents();
    }
}
