using FinDocAnalyzer.Core.Interfaces;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;
using UglyToad.PdfPig.Core;
using System.Text;

namespace FinDocAnalyzer.Infrastructure.Pdf;

public class PdfPigExtractor : IPdfExtractor
{
    public async Task<string> ExtractTextAsync(byte[] pdfContent)
    {
        // Task.Run to not block the thread (PdfPig is synchronous)
        return await Task.Run(() =>
        {
            try
            {
                var textBuilder = new StringBuilder();

                // Open PDF from byte array
                using (var document = PdfDocument.Open(pdfContent))
                {
                    // Add document metadata (context for AI)
                    textBuilder.AppendLine("=== DOCUMENT ===");
                    textBuilder.AppendLine($"Title: {document.Information.Title ?? "N/A"}");
                    textBuilder.AppendLine($"Author: {document.Information.Author ?? "N/A"}");
                    textBuilder.AppendLine($"Total Pages: {document.NumberOfPages}");
                    textBuilder.AppendLine();

                    // Iterate through each page
                    foreach (Page page in document.GetPages())
                    {
                        // Page header with structural context
                        textBuilder.AppendLine($"--- PAGE {page.Number} of {document.NumberOfPages} ---");
                        textBuilder.AppendLine($"Dimensions: {page.Width:F0}x{page.Height:F0} points");
                        textBuilder.AppendLine();

                        // ✨ IMPROVEMENT: Extract text preserving tabular structure
                        var pageText = ExtractPageWithTableDetection(page);
                        textBuilder.AppendLine(pageText);
                        
                        textBuilder.AppendLine();
                        textBuilder.AppendLine("--- END OF PAGE ---");
                        textBuilder.AppendLine(); // Blank line between pages
                    }
                }

                var extractedText = textBuilder.ToString();

                // Basic validation
                if (string.IsNullOrWhiteSpace(extractedText))
                {
                    throw new InvalidOperationException("No text was extracted from PDF. The file may be empty or contain only images.");
                }

                return extractedText;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error extracting text from PDF: {ex.Message}", ex);
            }
        });
    }

    /// <summary>
    /// Extract text from page with table detection and preservation of tabular structures
    /// </summary>
    private string ExtractPageWithTableDetection(Page page)
    {
        var words = page.GetWords().OrderBy(w => -w.BoundingBox.Bottom).ThenBy(w => w.BoundingBox.Left).ToList();
        
        if (!words.Any())
            return string.Empty;

        var textBuilder = new StringBuilder();
        var currentY = words.First().BoundingBox.Bottom;
        const double lineThreshold = 5.0; // Pixels de tolerância para mesma linha
        var currentLine = new List<Word>();

        foreach (var word in words)
        {
            // If line changed (different Y)
            if (Math.Abs(word.BoundingBox.Bottom - currentY) > lineThreshold)
            {
                // Process previous line
                if (currentLine.Any())
                {
                    AppendLine(textBuilder, currentLine);
                    currentLine.Clear();
                }
                currentY = word.BoundingBox.Bottom;
            }

            currentLine.Add(word);
        }

        // Process last line
        if (currentLine.Any())
        {
            AppendLine(textBuilder, currentLine);
        }

        return textBuilder.ToString();
    }

    /// <summary>
    /// Add a line of words preserving horizontal alignment
    /// </summary>
    private void AppendLine(StringBuilder builder, List<Word> words)
    {
        if (!words.Any()) return;

        // Sort words by X position (left to right)
        var sortedWords = words.OrderBy(w => w.BoundingBox.Left).ToList();
        
        // Detect if it's likely a table row (multiple aligned numbers)
        bool isTableRow = DetectTableRow(sortedWords);

        if (isTableRow)
        {
            builder.Append("[TABLE_ROW] ");
        }

        double lastX = 0;
        foreach (var word in sortedWords)
        {
            double gap = word.BoundingBox.Left - lastX;
            
            // If there's significant gap (>30 pixels), add proportional spacing
            if (gap > 30 && lastX > 0)
            {
                int spaces = Math.Min((int)(gap / 10), 10); // Max 10 spaces
                builder.Append(new string(' ', spaces));
            }
            else if (lastX > 0)
            {
                builder.Append(' '); // Normal space between words
            }

            builder.Append(word.Text);
            lastX = word.BoundingBox.Right;
        }

        if (isTableRow)
        {
            builder.Append(" [/TABLE_ROW]");
        }

        builder.AppendLine();
    }

    /// <summary>
    /// Detect if a line is likely part of a table (many numbers, alignment)
    /// </summary>
    private bool DetectTableRow(List<Word> words)
    {
        if (words.Count < 3) return false;

        // Count how many elements are numeric or monetary values
        int numericCount = 0;
        int totalWords = words.Count;

        foreach (var word in words)
        {
            string text = word.Text.Trim();
            
            // Remove common characters in financial values
            string cleanText = text.Replace(",", "").Replace("$", "").Replace("%", "")
                                   .Replace("(", "").Replace(")", "").Replace("-", "");

            if (double.TryParse(cleanText, out _))
            {
                numericCount++;
            }
        }

        // If >40% of elements are numeric, it's likely a table row
        return (double)numericCount / totalWords > 0.4;
    }

    public bool IsValidPdf(byte[] content)
    {
        // Basic validations
        if (content == null || content.Length < 5)
            return false;

        // Every valid PDF starts with "%PDF-" (bytes: 0x25 0x50 0x44 0x46 0x2D)
        return content[0] == 0x25 &&  // %
               content[1] == 0x50 &&  // P
               content[2] == 0x44 &&  // D
               content[3] == 0x46;    // F
    }
}