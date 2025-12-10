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
        // Task.Run para não bloquear a thread (PdfPig é síncrono)
        return await Task.Run(() =>
        {
            try
            {
                var textBuilder = new StringBuilder();

                // Abre o PDF a partir do array de bytes
                using (var document = PdfDocument.Open(pdfContent))
                {
                    // Adiciona metadados do documento (contexto para IA)
                    textBuilder.AppendLine("=== DOCUMENTO ===");
                    textBuilder.AppendLine($"Título: {document.Information.Title ?? "N/A"}");
                    textBuilder.AppendLine($"Autor: {document.Information.Author ?? "N/A"}");
                    textBuilder.AppendLine($"Total de Páginas: {document.NumberOfPages}");
                    textBuilder.AppendLine();

                    // Percorre cada página
                    foreach (Page page in document.GetPages())
                    {
                        // Cabeçalho da página com contexto estrutural
                        textBuilder.AppendLine($"--- PÁGINA {page.Number} de {document.NumberOfPages} ---");
                        textBuilder.AppendLine($"Dimensões: {page.Width:F0}x{page.Height:F0} pontos");
                        textBuilder.AppendLine();

                        // ✨ MELHORIA: Extrai texto preservando estrutura tabular
                        var pageText = ExtractPageWithTableDetection(page);
                        textBuilder.AppendLine(pageText);
                        
                        textBuilder.AppendLine();
                        textBuilder.AppendLine("--- FIM DA PÁGINA ---");
                        textBuilder.AppendLine(); // Linha em branco entre páginas
                    }
                }

                var extractedText = textBuilder.ToString();

                // Validação básica
                if (string.IsNullOrWhiteSpace(extractedText))
                {
                    throw new InvalidOperationException("Nenhum texto foi extraído do PDF. O arquivo pode estar vazio ou ser apenas imagens.");
                }

                return extractedText;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Erro ao extrair texto do PDF: {ex.Message}", ex);
            }
        });
    }

    /// <summary>
    /// Extrai texto da página com detecção e preservação de estruturas tabulares
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
            // Se mudou de linha (Y diferente)
            if (Math.Abs(word.BoundingBox.Bottom - currentY) > lineThreshold)
            {
                // Processa linha anterior
                if (currentLine.Any())
                {
                    AppendLine(textBuilder, currentLine);
                    currentLine.Clear();
                }
                currentY = word.BoundingBox.Bottom;
            }

            currentLine.Add(word);
        }

        // Processa última linha
        if (currentLine.Any())
        {
            AppendLine(textBuilder, currentLine);
        }

        return textBuilder.ToString();
    }

    /// <summary>
    /// Adiciona uma linha de palavras preservando alinhamento horizontal
    /// </summary>
    private void AppendLine(StringBuilder builder, List<Word> words)
    {
        if (!words.Any()) return;

        // Ordena palavras por posição X (esquerda para direita)
        var sortedWords = words.OrderBy(w => w.BoundingBox.Left).ToList();
        
        // Detecta se é provável linha de tabela (múltiplos números alinhados)
        bool isTableRow = DetectTableRow(sortedWords);

        if (isTableRow)
        {
            builder.Append("[TABLE_ROW] ");
        }

        double lastX = 0;
        foreach (var word in sortedWords)
        {
            double gap = word.BoundingBox.Left - lastX;
            
            // Se há gap significativo (>30 pixels), adiciona espaçamento proporcional
            if (gap > 30 && lastX > 0)
            {
                int spaces = Math.Min((int)(gap / 10), 10); // Max 10 espaços
                builder.Append(new string(' ', spaces));
            }
            else if (lastX > 0)
            {
                builder.Append(' '); // Espaço normal entre palavras
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
    /// Detecta se uma linha é provável parte de tabela (muitos números, alinhamento)
    /// </summary>
    private bool DetectTableRow(List<Word> words)
    {
        if (words.Count < 3) return false;

        // Conta quantos elementos são numéricos ou valores monetários
        int numericCount = 0;
        int totalWords = words.Count;

        foreach (var word in words)
        {
            string text = word.Text.Trim();
            
            // Remove caracteres comuns em valores financeiros
            string cleanText = text.Replace(",", "").Replace("$", "").Replace("%", "")
                                   .Replace("(", "").Replace(")", "").Replace("-", "");

            if (double.TryParse(cleanText, out _))
            {
                numericCount++;
            }
        }

        // Se >40% dos elementos são numéricos, provavelmente é linha de tabela
        return (double)numericCount / totalWords > 0.4;
    }

    public bool IsValidPdf(byte[] content)
    {
        // Validações básicas
        if (content == null || content.Length < 5)
            return false;

        // Todo PDF válido começa com "%PDF-" (bytes: 0x25 0x50 0x44 0x46 0x2D)
        return content[0] == 0x25 &&  // %
               content[1] == 0x50 &&  // P
               content[2] == 0x44 &&  // D
               content[3] == 0x46;    // F
    }
}