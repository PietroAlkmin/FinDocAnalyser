using FinDocAnalyzer.Core.Interfaces;
using FinDocAnalyzer.Core.Models;
using OpenAI.Chat;
using System.Text.Json;

namespace FinDocAnalyzer.Infrastructure.AI;

public class OpenAiAnalyzer : IAiAnalyzer
{
    private readonly ChatClient _chatClient;
    private const int MaxTextLength = 100000; // Limite do GPT-4

    public OpenAiAnalyzer(ChatClient chatClient)
    {
        _chatClient = chatClient;
    }

    public async Task<AnalysisResult> AnalyzeAsync(string extractedText)
    {
        try
        {
            // Trunca o texto se for muito longo
            var textToAnalyze = extractedText.Length > MaxTextLength
                ? extractedText.Substring(0, MaxTextLength)
                : extractedText;

            // Cria o prompt do sistema (instrui o AI sobre seu papel)
            var systemPrompt = CreateSystemPrompt();

            // Cria o prompt do usuário (o texto a analisar)
            var userPrompt = $@"Analise este relatório financeiro e extraia os dados estruturados:

{textToAnalyze}

Retorne um JSON válido seguindo exatamente o schema definido.";

            // Configura a chamada da API
            var chatOptions = new ChatCompletionOptions
            {
                ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat(),
                Temperature = 0.1f, // Baixa temperatura = mais preciso, menos criativo
                MaxOutputTokenCount = 4000
            };

            // Cria as mensagens
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(systemPrompt),
                new UserChatMessage(userPrompt)
            };

            // Chama a API da OpenAI
            var completion = await _chatClient.CompleteChatAsync(messages, chatOptions);
            var responseContent = completion.Value.Content[0].Text;

            // Parse do JSON retornado
            var extractedData = ParseAiResponse(responseContent);

            // Cria o resultado final
            var result = new AnalysisResult
            {
                AnalysisId = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddMinutes(30),
                Total = extractedData.Total,
                Classification = extractedData.Classification,
                Stocks = extractedData.Stocks,
                FixedIncome = extractedData.FixedIncome
            };

            return result;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Erro ao analisar documento com IA: {ex.Message}", ex);
        }
    }

    private string CreateSystemPrompt()
    {
        return @"Você é um especialista em análise de relatórios financeiros brasileiros. Sua tarefa é extrair dados estruturados de relatórios de investimentos.

IMPORTANTE: Retorne APENAS um objeto JSON válido, sem texto adicional antes ou depois.

O JSON deve seguir EXATAMENTE esta estrutura:

{
  ""total"": {
    ""totalInvestedAmount"": number,
    ""currency"": ""BRL""
  },
  ""classification"": {
    ""totalInvested"": number,
    ""currency"": ""BRL"",
    ""classes"": [
      {
        ""assetClassName"": string,
        ""invested"": number,
        ""percentage"": number,
        ""confidence"": number (0.0 a 1.0),
        ""confidenceReason"": string
      }
    ]
  },
  ""stocks"": {
    ""totalInvested"": number,
    ""currency"": ""BRL"",
    ""stocks"": [
      {
        ""ticker"": string,
        ""quantity"": number,
        ""averagePrice"": number,
        ""totalInvested"": number,
        ""currentValue"": number,
        ""return"": number ou null,
        ""returnPercentage"": number ou null,
        ""confidence"": number,
        ""confidenceReason"": string
      }
    ]
  },
  ""fixedIncome"": {
    ""totalInvested"": number,
    ""currency"": ""BRL"",
    ""assets"": [
      {
        ""name"": string,
        ""type"": string,
        ""issuer"": string,
        ""investedAmount"": number,
        ""currentValue"": number ou null,
        ""return"": number ou null,
        ""returnPercentage"": number ou null,
        ""rate"": string,
        ""maturityDate"": ""YYYY-MM-DD"" ou null,
        ""applicationDate"": ""YYYY-MM-DD"" ou null,
        ""confidence"": number,
        ""confidenceReason"": string
      }
    ]
  }
}

REGRAS para extração:
1. Extraia TODOS os valores numéricos sem símbolos de moeda ou separadores de milhar
2. Use ponto (.) como separador decimal
3. Para percentuais, use o valor decimal (ex: 42.6 para 42,6%)
4. Datas no formato ISO (YYYY-MM-DD)
5. Se um dado não estiver disponível, use null (exceto para campos obrigatórios)
6. Campos OBRIGATÓRIOS (nunca null):
   - totalInvestedAmount, invested, percentage, quantity, averagePrice, totalInvested, investedAmount, confidence
7. Campos OPCIONAIS (podem ser null):
   - return, returnPercentage, currentValue, maturityDate, applicationDate
8. Confidence scores: 
   - 0.95-1.0: Dados em tabelas estruturadas com labels claros
   - 0.85-0.94: Dados identificáveis mas requerem interpretação
   - 0.70-0.84: Dados inferidos do contexto
   - 0.50-0.69: Dados incertos ou estimados
   - < 0.50: Use null ao invés do valor (para campos opcionais)
9. ConfidenceReason: Explique BREVEMENTE por que atribuiu essa confiança

Tipos de ativos brasileiros comuns:
- Ações: tickers terminam em números (ex: PETR4, VALE3)
- ETFs: tickers terminam em 11 (ex: BOVA11, IVVB11)
- Fundos: nomes longos com siglas (ex: TREND DI FIC RF)
- Renda Fixa: LCI, LCA, CDB, Debêntures, CRI, CRA
- Tesouro: NTNB, NTN-B, LTN, LFT

Seja preciso e consistente. A qualidade dos dados é crítica.";
    }

    private ExtractedData ParseAiResponse(string jsonResponse)
    {
        try
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var data = JsonSerializer.Deserialize<ExtractedData>(jsonResponse, options);

            if (data == null)
            {
                throw new InvalidOperationException("AI retornou resposta vazia ou inválida");
            }

            return data;
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Erro ao fazer parse do JSON retornado pela IA: {ex.Message}", ex);
        }
    }

    // Classe auxiliar para deserialização
    private class ExtractedData
    {
        public TotalInvested Total { get; set; } = new();
        public AssetClassification Classification { get; set; } = new();
        public StockPortfolio Stocks { get; set; } = new();
        public FixedIncomePortfolio FixedIncome { get; set; } = new();
    }
}
