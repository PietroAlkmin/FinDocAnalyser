using FinDocAnalyzer.Core.Interfaces;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
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
                    // Percorre cada página
                    foreach (Page page in document.GetPages())
                    {
                        // Adiciona separador de página (útil para contexto)
                        textBuilder.AppendLine($"--- Página {page.Number} ---");

                        // Extrai o texto da página
                        var pageText = page.Text;
                        textBuilder.AppendLine(pageText);
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