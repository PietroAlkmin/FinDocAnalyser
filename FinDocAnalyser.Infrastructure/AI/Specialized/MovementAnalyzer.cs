using FinDocAnalyzer.Core.Interfaces;
using FinDocAnalyzer.Core.Models;
using Microsoft.Extensions.AI;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace FinDocAnalyzer.Infrastructure.AI.Specialized;

/// <summary>
/// Analyzes financial movements/transactions from extracted PDF text
/// Detects: applications, redemptions, purchases, sales, dividends, JCP, etc.
/// </summary>
public class MovementAnalyzer : ISpecializedAnalyzer<MovementsAnalysis>
{
    private readonly IChatClient _chatClient;

    public string Category => "Movements";
    public bool IsCritical => false; // Movement analysis is optional

    public MovementAnalyzer(IChatClient chatClient)
    {
        _chatClient = chatClient;
    }

    public async Task<MovementsAnalysis?> AnalyzeAsync(string extractedText)
    {
        var prompt = BuildMovementPrompt(extractedText);
        
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, GetSystemPrompt()),
            new(ChatRole.User, prompt)
        };

        var response = await _chatClient.CompleteAsync(messages);
        var jsonResponse = ExtractJson(response.Message.Text ?? "{}");
        
        var movements = JsonSerializer.Deserialize<MovementsAnalysis>(
            jsonResponse,
            new JsonSerializerOptions 
            { 
                PropertyNameCaseInsensitive = true,
                NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString
            }
        ) ?? new MovementsAnalysis();

        // Calculate totals
        movements.TotalApplications = movements.Movements
            .Where(m => m.Type.Contains("Aplicação", StringComparison.OrdinalIgnoreCase) || 
                       m.Type.Contains("Compra", StringComparison.OrdinalIgnoreCase))
            .Sum(m => m.GrossAmount);

        movements.TotalRedemptions = movements.Movements
            .Where(m => m.Type.Contains("Resgate", StringComparison.OrdinalIgnoreCase) || 
                       m.Type.Contains("Venda", StringComparison.OrdinalIgnoreCase))
            .Sum(m => m.GrossAmount);

        movements.TotalDividends = movements.Movements
            .Where(m => m.Type.Contains("Dividendo", StringComparison.OrdinalIgnoreCase) || 
                       m.Type.Contains("JCP", StringComparison.OrdinalIgnoreCase) ||
                       m.Type.Contains("Juros", StringComparison.OrdinalIgnoreCase))
            .Sum(m => m.GrossAmount);

        movements.TotalTaxes = movements.Movements
            .Sum(m => (m.IncomeTax ?? 0) + (m.IOF ?? 0));

        movements.TotalFees = movements.Movements
            .Sum(m => m.Fees ?? 0);

        return movements;
    }

    private string GetSystemPrompt()
    {
        return @"You are a financial document analyzer specialized in extracting transaction movements.

CRITICAL RULES:
1. Extract ALL movements/transactions from the document
2. Be AGNOSTIC to bank/brokerage format - adapt to any structure
3. Common movement types:
   - Aplicação (Application/Investment)
   - Resgate (Redemption/Withdrawal)
   - Compra (Purchase/Buy)
   - Venda (Sale/Sell)
   - Dividendo (Dividend)
   - JCP (Interest on Equity)
   - Transferência (Transfer)
   - Pagamento de juros (Interest Payment)

4. Extract dates in ISO format (YYYY-MM-DD)
5. Extract amounts as decimal numbers
6. If a field is not available, use null or empty
7. Return ONLY valid JSON, no markdown, no explanations

OUTPUT FORMAT:
{
  ""movements"": [
    {
      ""date"": ""2025-09-01"",
      ""type"": ""Aplicação"",
      ""description"": ""LCA-CDI ITAU"",
      ""asset"": ""LCA-CDI"",
      ""grossAmount"": 300000.00,
      ""netAmount"": 300000.00,
      ""quantity"": 300000,
      ""unitPrice"": 1.00,
      ""incomeTax"": 0,
      ""iof"": 0,
      ""fees"": 0,
      ""documentNumber"": ""6705636""
    }
  ]
}";
    }

    private string BuildMovementPrompt(string extractedText)
    {
        // Find movement sections
        var movementSections = ExtractMovementSections(extractedText);
        
        var sb = new StringBuilder();
        sb.AppendLine("Extract ALL financial movements from this document.");
        sb.AppendLine("Focus on sections containing: 'Movimentações', 'Extrato', 'Transações', 'Operações', etc.");
        sb.AppendLine();
        sb.AppendLine("DOCUMENT TEXT:");
        sb.AppendLine(movementSections);
        sb.AppendLine();
        sb.AppendLine("Return JSON with the movements array.");
        
        return sb.ToString();
    }

    private string ExtractMovementSections(string text)
    {
        // Try to find sections with movements
        var keywords = new[] { 
            "movimentações", "movimentação", "extrato", "transações", 
            "operações", "histórico", "aplicações", "resgates",
            "conta corrente", "notas", "liquidação"
        };

        var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        var relevantLines = new List<string>();
        var inRelevantSection = false;
        var linesAfterKeyword = 0;

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var lowerLine = line.ToLower();

            // Check if we hit a movement section
            if (keywords.Any(k => lowerLine.Contains(k)))
            {
                inRelevantSection = true;
                linesAfterKeyword = 0;
            }

            // Include lines in relevant sections
            if (inRelevantSection)
            {
                relevantLines.Add(line);
                linesAfterKeyword++;

                // Stop after reasonable amount of lines or if we hit a new major section
                if (linesAfterKeyword > 200 || 
                    (linesAfterKeyword > 30 && lowerLine.Contains("--- end of page ---")))
                {
                    inRelevantSection = false;
                }
            }
        }

        // If no specific section found, return a subset of the document
        if (relevantLines.Count == 0)
        {
            return string.Join("\n", lines.Take(500));
        }

        return string.Join("\n", relevantLines);
    }

    private string ExtractJson(string text)
    {
        // Remove markdown code blocks
        text = Regex.Replace(text, @"```json\s*", "", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"```\s*", "", RegexOptions.IgnoreCase);
        
        // Find JSON object
        var match = Regex.Match(text, @"\{[\s\S]*\}", RegexOptions.Multiline);
        if (match.Success)
        {
            return match.Value;
        }

        // Fallback: try to find array
        match = Regex.Match(text, @"\[[\s\S]*\]", RegexOptions.Multiline);
        if (match.Success)
        {
            return $"{{\"movements\": {match.Value}}}";
        }

        return "{}";
    }
}
