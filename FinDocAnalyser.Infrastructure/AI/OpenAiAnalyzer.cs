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
                Analysis = extractedData.Analysis, // 🆕 Análises calculadas pela AI
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
  },
  ""analysis"": {
    ""totalReturn"": number (CALCULE: soma de todos os returns de stocks + fixedIncome),
    ""totalReturnPercentage"": number (CALCULE: (totalReturn / totalInvestedAmount) × 100),
    ""bestAsset"": string ou null (ticker ou nome do ativo com MAIOR returnPercentage),
    ""bestAssetReturn"": number ou null (returnPercentage do melhor ativo),
    ""worstAsset"": string ou null (ticker ou nome do ativo com MENOR returnPercentage),
    ""worstAssetReturn"": number ou null (returnPercentage do pior ativo),
    ""uniqueIssuers"": number (CONTE: quantos emissores diferentes - empresas de ações + emissores de renda fixa),
    ""uniqueAssetTypes"": number (CONTE: quantos tipos diferentes - stocks, bonds, CDB, LCI, etc),
    ""concentrationRisk"": number (CALCULE: % do ativo individual de MAIOR currentValue em relação ao total - use formato DECIMAL 0.0-100.0, ex: 15.5 para 15.5%),
    ""mostConcentratedAsset"": string ou null (nome/ticker do ativo de maior valor),
    ""highLiquidityPercentage"": number (ESTIME: % em caixa, fundos DI, ativos D+0 - formato DECIMAL 0.0-100.0),
    ""mediumLiquidityPercentage"": number (ESTIME: % em ações líquidas, ETFs - formato DECIMAL 0.0-100.0),
    ""lowLiquidityPercentage"": number (ESTIME: % em renda fixa com vencimento, imóveis, etc - formato DECIMAL 0.0-100.0),
    ""notes"": string ou null (observações relevantes - ex: ""Portfolio concentrado em tech stocks""),
    ""warnings"": [string] (alertas - ex: [""Preço médio não disponível para PETR4"", ""Data de vencimento ausente em 3 CDBs""]),
    ""confidenceScore"": number (AVALIE: confiança geral da análise, 0.0 a 1.0)
  }
}

INSTRUÇÕES CRÍTICAS PARA A SEÇÃO ""analysis"":

1. CALCULE os retornos totais:
   - Some TODOS os ""return"" de stocks e fixedIncome
   - Se algum ""return"" for null, ignore-o na soma (não conte como zero)
   - Calcule o % dividindo pelo totalInvestedAmount

2. IDENTIFIQUE melhor e pior ativos:
   - Compare os ""returnPercentage"" de TODOS os ativos
   - Use o ticker para stocks, o ""name"" para fixedIncome
   - Se não houver returnPercentage, use null

3. ANALISE diversificação:
   - Count issuers: Para stocks use o ticker (cada ticker = 1 emissor), para fixedIncome use o campo ""issuer""
   - Count asset types: CDB, LCI, ações, bonds, etc são tipos diferentes
   - Concentration: Ache o ativo individual de MAIOR ""currentValue"", divida pelo totalInvestedAmount, multiplique por 100
   - Exemplo: Se maior ativo = 5000 e total = 32000, então concentrationRisk = (5000/32000) × 100 = 15.625

4. ESTIME liquidez (use seu conhecimento financeiro):
   - Alta: Caixa, Fundos DI, Tesouro Selic
   - Média: Ações líquidas (grande cap), ETFs
   - Baixa: CDB/LCI com prazo, Debêntures, small caps

5. GERE warnings usando os dados REAIS do relatório:
   - Liste APENAS problemas encontrados nos dados DESTE relatório específico
   - Mencione tickers/nomes REAIS quando houver problemas (ex: ""Preço médio ausente para [ticker real]"")
   - NÃO use exemplos genéricos - apenas dados reais extraídos
   - Ex válido: ""Data de vencimento ausente em 3 CDBs"" (se realmente faltarem)
   - Ex INVÁLIDO: ""Preço médio não disponível para PETR4"" (se PETR4 não existir no relatório)

6. AVALIE confidence geral:
   - Se TODOS os dados vieram de tabelas claras: 0.95+
   - Se alguns dados foram inferidos: 0.80-0.94
   - Se muitos dados estão faltando: 0.60-0.79
   - Se o relatório é confuso/incompleto: < 0.60

SEJA RIGOROSO: Use null quando não tiver certeza, mas SEMPRE tente calcular quando os dados necessários estiverem disponíveis.


1. DETECÇÃO AUTOMÁTICA:
   - Detecte automaticamente a moeda do relatório (R$, $, €, £, USD, BRL, etc)
   - Identifique se é brasileiro (B3, Bovespa) ou internacional (NYSE, NASDAQ, etc)
   - Reconheça diferentes formatos de data e converta para ISO (YYYY-MM-DD)

2. VALORES NUMÉRICOS:
   - Extraia SEM símbolos de moeda ou separadores de milhar
   - Use ponto (.) como separador decimal sempre
   - Para percentuais, use o valor decimal (ex: 42.6 para 42,6%)

3. TICKERS/CÓDIGOS:
   - Brasil: tickers terminam em números (XXXX3, YYYY4, ZZZZ11)
   - EUA: sem sufixos numéricos (XXXX, YYYY, ZZZZ)
   - Offshore: pode ter variações (ADRs, etc)

4. TIPOS DE ATIVOS (reconheça automaticamente):
   
   BRASIL:
   - Ações: formato XXXX3, YYYY4, ZZZZ32 (IMPORTANTE: mantenha ticker COMPLETO!)
   - ETFs: formato XXXX11, YYYY11
   - Fundos: nomes longos descritivos
   - Renda Fixa: LCI, LCA, CDB, Debêntures, CRI, CRA, Tesouro Direto
   
   INTERNACIONAL:
   - Stocks: formato alfabético sem sufixo numérico
   - ETFs: formato alfabético curto (3-4 letras)
   - Bonds: Treasury, Corporate, Municipal
   - Mutual Funds: Vanguard, Fidelity, etc
   
   OFFSHORE:
   - Pode combinar ativos globais
   - Valores em USD, EUR, CHF
   - ADRs podem usar formatos variados

   REGRA CRÍTICA PARA TICKERS:
   - Copie o ticker EXATAMENTE como aparece no PDF (sem truncar ou modificar)
   - Tickers podem ter 4-6 caracteres - preserve TODOS os dígitos/sufixos
   - Inclua todos os sufixos (F, B, números, etc.) - exemplo: XXXX32 não é XXXX3

5. CONFIDENCE SCORES (seja rigoroso):
   - 0.95-1.0: Dados em tabelas estruturadas com labels explícitos
   - 0.85-0.94: Dados claramente identificáveis mas requerem interpretação
   - 0.70-0.84: Dados inferidos do contexto geral
   - 0.50-0.69: Dados incertos ou estimados
   - < 0.50: NÃO INCLUA - use null

6. CONFIDENCE REASON:
   - Explique BREVEMENTE por que atribuiu essa confiança
   - Ex: ""Valor em tabela estruturada"", ""Inferido do saldo total"", ""Código identificado no cabeçalho""

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
        public PortfolioAnalysis? Analysis { get; set; }
    }
}
