using FinDocAnalyzer.Core.Interfaces;
using FinDocAnalyzer.Core.Models;
using Microsoft.Extensions.AI;
using System.Text.Json;
using System.Diagnostics;

namespace FinDocAnalyzer.Infrastructure.AI;

/// <summary>
/// Analisador de documentos financeiros usando Microsoft.Extensions.AI
/// </summary>
public class AiDocumentAnalyzer : IAiAnalyzer
{
    private readonly IChatClient _chatClient;
    private const int MaxTextLength = 120000; // GPT-4o suporta mais tokens
    private const string DefaultModel = "gpt-4o";

    public AiDocumentAnalyzer(IChatClient chatClient)
    {
        _chatClient = chatClient;
    }

    public async Task<AnalysisResult> AnalyzeAsync(string extractedText)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            // Trunca o texto se for muito longo
            var textToAnalyze = extractedText.Length > MaxTextLength
                ? extractedText[..MaxTextLength]
                : extractedText;

            // Conta páginas processadas (estimativa)
            var pageCount = CountPages(extractedText);

            // Cria o prompt universal e inteligente
            var systemPrompt = CreateUniversalPrompt();
            var userPrompt = $@"Analise este relatório financeiro e extraia TODOS os dados estruturados disponíveis:

{textToAnalyze}

Retorne um JSON válido seguindo exatamente o schema definido.";

            // Configura a chamada com Microsoft.Extensions.AI
            var chatOptions = new ChatOptions
            {
                ResponseFormat = ChatResponseFormat.Json,
                Temperature = 0.1f, // Precisão máxima
                MaxOutputTokens = 4096,
                ModelId = DefaultModel
            };

            // Cria as mensagens
            var messages = new List<ChatMessage>
            {
                new(ChatRole.System, systemPrompt),
                new(ChatRole.User, userPrompt)
            };

            // Chama a IA (abstração Microsoft.Extensions.AI)
            var response = await _chatClient.CompleteAsync(messages, chatOptions);
            
            stopwatch.Stop();

            // Extrai tokens e modelo usado
            var tokensUsed = response.Usage?.TotalTokenCount ?? 0;
            var modelUsed = response.ModelId ?? DefaultModel;

            // Parse do JSON retornado
            var extractedData = ParseAiResponse(response.Message.Text ?? string.Empty);

            // Calcula custo estimado (GPT-4o pricing)
            var estimatedCost = CalculateCost(tokensUsed, modelUsed);

            // Cria o resultado final com metadados completos
            var result = new AnalysisResult
            {
                AnalysisId = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddMinutes(30),
                Total = extractedData.Total,
                Classification = extractedData.Classification,
                Stocks = extractedData.Stocks,
                FixedIncome = extractedData.FixedIncome,
                Metadata = new AnalysisMetadata
                {
                    ReportType = DetectReportType(extractedText),
                    TokensUsed = tokensUsed,
                    EstimatedCost = estimatedCost,
                    AiModel = modelUsed,
                    AiProvider = "Microsoft.Extensions.AI",
                    PagesProcessed = pageCount,
                    FromCache = false
                },
                Audit = new AuditInfo
                {
                    ProcessedAt = DateTime.UtcNow,
                    ProcessingDuration = stopwatch.Elapsed
                }
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
        return CreateUniversalPrompt();
    }

    private string CreateUniversalPrompt()
    {
        return @"Você é um especialista GLOBAL em análise de relatórios financeiros (brasileiros, internacionais, offshore). 

Sua tarefa é extrair dados estruturados de QUALQUER tipo de relatório de investimentos, independente do formato, instituição ou país.

IMPORTANTE: Retorne APENAS um objeto JSON válido, sem texto adicional antes ou depois.

O JSON deve seguir EXATAMENTE esta estrutura:

{
  ""total"": {
    ""totalInvestedAmount"": number,
    ""currency"": string (ex: ""BRL"", ""USD"", ""EUR"")
  },
  ""classification"": {
    ""totalInvested"": number,
    ""currency"": string,
    ""classes"": [
      {
        ""assetClassName"": string (ex: ""Renda Fixa"", ""Ações"", ""Fundos"", ""Stocks"", ""Bonds""),
        ""invested"": number,
        ""percentage"": number,
        ""confidence"": number (0.0 a 1.0),
        ""confidenceReason"": string
      }
    ]
  },
  ""stocks"": {
    ""totalInvested"": number,
    ""currency"": string,
    ""stocks"": [
      {
        ""ticker"": string (ex: ""PETR4"", ""AAPL"", ""GOOGL""),
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
    ""currency"": string,
    ""assets"": [
      {
        ""name"": string,
        ""type"": string (ex: ""CDB"", ""LCI"", ""Treasury Bond"", ""Debenture""),
        ""issuer"": string,
        ""investedAmount"": number,
        ""currentValue"": number,
        ""return"": number,
        ""returnPercentage"": number,
        ""rate"": string (ex: ""110% CDI"", ""5.5% a.a."", ""3.25% coupon""),
        ""maturityDate"": ""YYYY-MM-DD"" ou null,
        ""applicationDate"": ""YYYY-MM-DD"" ou null,
        ""confidence"": number,
        ""confidenceReason"": string
      }
    ]
  }
}

REGRAS UNIVERSAIS para extração:

1. DETECÇÃO AUTOMÁTICA:
   - Detecte automaticamente a moeda do relatório (R$, $, €, £, USD, BRL, etc)
   - Identifique se é brasileiro (B3, Bovespa) ou internacional (NYSE, NASDAQ, etc)
   - Reconheça diferentes formatos de data e converta para ISO (YYYY-MM-DD)

2. VALORES NUMÉRICOS:
   - Extraia SEM símbolos de moeda ou separadores de milhar
   - Use ponto (.) como separador decimal sempre
   - Para percentuais, use o valor decimal (ex: 42.6 para 42,6%)

3. TICKERS/CÓDIGOS:
   - Brasil: tickers terminam em números (PETR4, VALE3, BOVA11)
   - EUA: sem sufixos numéricos (AAPL, GOOGL, TSLA)
   - Offshore: pode ter variações (ADRs, etc)

4. TIPOS DE ATIVOS (reconheça automaticamente):
   
   BRASIL:
   - Ações: PETR4, VALE3, ITUB4
   - ETFs: BOVA11, IVVB11, SMAL11
   - Fundos: nomes longos (TREND DI FIC RF)
   - Renda Fixa: LCI, LCA, CDB, Debêntures, CRI, CRA, Tesouro Direto
   
   INTERNACIONAL:
   - Stocks: AAPL, GOOGL, MSFT, TSLA
   - ETFs: SPY, VOO, QQQ
   - Bonds: Treasury, Corporate, Municipal
   - Mutual Funds: Vanguard, Fidelity, etc
   
   OFFSHORE:
   - Pode combinar ativos globais
   - Valores em USD, EUR, CHF
   - ADRs brasileiros: VALE, PBR

5. CONFIDENCE SCORES (seja rigoroso):
   - 0.95-1.0: Dados em tabelas estruturadas com labels explícitos
   - 0.85-0.94: Dados claramente identificáveis mas requerem interpretação
   - 0.70-0.84: Dados inferidos do contexto geral
   - 0.50-0.69: Dados incertos ou estimados
   - < 0.50: NÃO INCLUA - use null

6. CONFIDENCE REASON:
   - Explique BREVEMENTE por que atribuiu essa confiança
   - Ex: ""Valor em tabela estruturada"", ""Inferido do saldo total"", ""Ticker identificado no cabeçalho""

7. DADOS AUSENTES:
   - Se um dado não estiver disponível, use null
   - NÃO invente valores
   - Arrays vazios [] se não houver dados da categoria

8. ADAPTABILIDADE:
   - Funcione para QUALQUER formato: PDF de corretora, banco, offshore, consolidado
   - Ignore cabeçalhos, rodapés, informações irrelevantes
   - Foque apenas nos dados de investimentos

Seja preciso, consistente e adaptável. A qualidade dos dados é crítica.";
    }

    private int CountPages(string extractedText)
    {
        // Conta separadores de página inseridos pelo PdfExtractor
        return extractedText.Split("--- Página", StringSplitOptions.RemoveEmptyEntries).Length - 1;
    }

    private string DetectReportType(string extractedText)
    {
        var lowerText = extractedText.ToLowerInvariant();

        // Detecta tipo baseado em palavras-chave
        if (lowerText.Contains("bradesco")) return "Bradesco";
        if (lowerText.Contains("itaú") || lowerText.Contains("itau")) return "Itaú";
        if (lowerText.Contains("xp investimentos") || lowerText.Contains("xp inc")) return "XP";
        if (lowerText.Contains("btg pactual")) return "BTG";
        if (lowerText.Contains("nubank") || lowerText.Contains("nu invest")) return "Nubank";
        if (lowerText.Contains("inter") && lowerText.Contains("invest")) return "Inter";
        if (lowerText.Contains("offshore") || lowerText.Contains("cayman") || lowerText.Contains("bahamas")) return "Offshore";
        if (lowerText.Contains("usd") || lowerText.Contains("us$") || lowerText.Contains("dollar")) return "International";

        return "Generic";
    }

    private decimal CalculateCost(int tokens, string model)
    {
        // Preços aproximados (Nov 2025)
        var (inputCost, outputCost) = model.ToLowerInvariant() switch
        {
            var m when m.Contains("gpt-4o") => (2.50m / 1_000_000, 10.00m / 1_000_000),
            var m when m.Contains("gpt-4o-mini") => (0.15m / 1_000_000, 0.60m / 1_000_000),
            var m when m.Contains("gpt-4") => (30.00m / 1_000_000, 60.00m / 1_000_000),
            _ => (2.50m / 1_000_000, 10.00m / 1_000_000) // Default GPT-4o
        };

        // Estimativa simples (70% input, 30% output)
        var inputTokens = (int)(tokens * 0.7);
        var outputTokens = (int)(tokens * 0.3);

        return (inputTokens * inputCost) + (outputTokens * outputCost);
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
