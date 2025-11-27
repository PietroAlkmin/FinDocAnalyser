using FinDocAnalyzer.Core.Interfaces;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;
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

                        // ✨ MELHORIA: Usa ContentOrderTextExtractor para ordem de leitura correta
                        // Mantém a ordem visual correta (especialmente importante em múltiplas colunas)
                        var pageText = ContentOrderTextExtractor.GetText(page);
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