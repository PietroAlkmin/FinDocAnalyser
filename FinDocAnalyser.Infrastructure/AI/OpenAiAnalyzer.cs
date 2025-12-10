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
            var rawResponse = response.Message.Text ?? string.Empty;
            var extractedData = ParseAiResponse(rawResponse);

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
                VariableIncome = extractedData.VariableIncome,
                FixedIncome = extractedData.FixedIncome,
                AlternativeAssets = extractedData.AlternativeAssets,
                Cash = extractedData.Cash,
                // DEPRECATED: Manter compatibilidade - mapear VariableIncome para Stocks
#pragma warning disable CS0618 // Type or member is obsolete
                Stocks = extractedData.Stocks ?? new StockPortfolio 
                { 
                    TotalInvested = extractedData.VariableIncome?.TotalInvested ?? 0,
                    Currency = extractedData.VariableIncome?.Currency ?? "BRL",
                    Stocks = extractedData.VariableIncome?.Assets?.Select(a => new StockHolding
                    {
                        Ticker = a.Ticker ?? "",
                        Quantity = (int)a.Quantity, // Conversão de decimal para int
                        AveragePrice = a.AveragePrice,
                        CurrentValue = a.CurrentValue,
                        Return = a.Return,
                        ReturnPercentage = a.ReturnPercentage,
                        Yield = a.Yield ?? "",
                        Confidence = a.Confidence,
                        ConfidenceReason = a.ConfidenceReason ?? ""
                    }).ToList() ?? new List<StockHolding>()
                },
#pragma warning restore CS0618 // Type or member is obsolete
                Metadata = new AnalysisMetadata
                {
                    ReportType = DetectReportType(extractedText),
                    TokensUsed = tokensUsed,
                    EstimatedCost = estimatedCost,
                    AiModel = modelUsed,
                    AiProvider = "Microsoft.Extensions.AI",
                    PagesProcessed = pageCount,
                    FromCache = false,
                    RawAiResponse = rawResponse,
                    Reasoning = extractedData.Reasoning
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
        ""assetClassName"": string (ex: ""Renda Variável"", ""Renda Fixa"", ""Ativos Alternativos"", ""Cash""),
        ""invested"": number,
        ""percentage"": number,
        ""confidence"": number (0.0 a 1.0),
        ""confidenceReason"": string
      }
    ]
  },
  ""variableIncome"": {
    ""totalInvested"": number,
    ""currency"": string,
    ""assets"": [
      {
        ""ticker"": string (ex: ""PETR4"", ""AAPL"", ""BOVA11""),
        ""name"": string (ex: ""Petrobras PN"", ""Apple Inc""),
        ""type"": string (""Stock"", ""ETF"", ""ADR"", ""BDR"", ""Option"", ""Future""),
        ""quantity"": number,
        ""averagePrice"": number,
        ""currentValue"": number,
        ""return"": number ou null,
        ""returnPercentage"": number ou null,
        ""yield"": string ou null,
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
        ""type"": string (ex: ""CDB"", ""LCI"", ""Treasury Bond"", ""Debenture"", ""Corporate Bond""),
        ""issuer"": string,
        ""investedAmount"": number,
        ""currentValue"": number,
        ""return"": number ou null,
        ""returnPercentage"": number ou null,
        ""rate"": string ou null,
        ""yield"": string ou null,
        ""maturityDate"": ""YYYY-MM-DD"" ou null,
        ""applicationDate"": ""YYYY-MM-DD"" ou null,
        ""confidence"": number,
        ""confidenceReason"": string
      }
    ]
  },
  ""alternativeAssets"": {
    ""totalInvested"": number,
    ""currency"": string,
    ""assets"": [
      {
        ""name"": string,
        ""type"": string (FLEXÍVEL: ""REIT"", ""FII"", ""PrivateEquity"", ""HedgeFund"", ""Cryptocurrency"", ""Commodity"", ""StructuredProduct"", ""Art"", ""VentureCapital"", ""RealAssets"", ""Infrastructure"", ou QUALQUER outra categoria),
        ""symbol"": string ou null (ticker/identificador se houver),
        ""issuer"": string ou null (gestor/administrador),
        ""description"": string ou null (descrição adicional, estratégia),
        ""quantity"": number ou null (cotas/unidades se aplicável),
        ""unitPrice"": number ou null (preço unitário se aplicável),
        ""investedAmount"": number,
        ""currentValue"": number,
        ""return"": number ou null,
        ""returnPercentage"": number ou null,
        ""yield"": string ou null (rendimento/distribuições),
        ""managementFee"": string ou null (taxa de administração),
        ""lockupPeriod"": string ou null (período de carencia/lock-up),
        ""inceptionDate"": ""YYYY-MM-DD"" ou null,
        ""maturityDate"": ""YYYY-MM-DD"" ou null,
        ""additionalData"": object ou null (chave-valor com dados extras específicos),
        ""confidence"": number,
        ""confidenceReason"": string
      }
    ]
  },
  ""cash"": {
    ""totalBalance"": number,
    ""currency"": string,
    ""positions"": [
      {
        ""name"": string (ex: ""Conta Corrente"", ""Deposit Sweep"", ""Money Market Fund""),
        ""type"": string (""CheckingAccount"", ""SavingsAccount"", ""MoneyMarket"", ""SweepAccount"", ""CD""),
        ""institution"": string,
        ""balance"": number,
        ""yield"": string ou null,
        ""currency"": string,
        ""isAvailable"": boolean,
        ""confidence"": number,
        ""confidenceReason"": string
      }
    ]
  },
  ""reasoning"": {
    ""documentAnalysis"": string (análise inicial do tipo de documento e estrutura),
    ""currencyDetection"": string (como detectou a moeda principal),
    ""categoryDecisions"": string (principais decisões de categorização),
    ""uncertainties"": [string] (pontos de incerteza ou ambiguidade),
    ""assumptions"": [string] (premissas assumidas durante análise),
    ""dataQualityAssessment"": string (avaliação da qualidade dos dados extraídos),
    ""specificDecisions"": {
      ""key"": string (decisões específicas importantes, ex: por que classificou X como Y)
    }
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

3. DISTINÇÃO CRÍTICA - VALOR INVESTIDO vs VALOR ATUAL:
   - investedAmount/averagePrice: Custo original do investimento (termos comuns: ""Original Cost"", ""Adjusted Cost"", ""Cost Basis"", ""Custo de Aquisição"", ""Valor Aplicado"", ""Invested"", ""Purchase Price"")
   - currentValue: Valor de mercado atual (termos comuns: ""Market Value"", ""Current Value"", ""Value"", ""Valor Atual"", ""Posição"", ""Position Value"")
   - return: Diferença entre currentValue e investedAmount (termos comuns: ""Gain/Loss"", ""Unrealized Gain/Loss"", ""Profit/Loss"", ""Lucro/Prejuízo"", ""Rentabilidade Absoluta"")
   - returnPercentage: (return / investedAmount) * 100
   - NUNCA use o mesmo valor para investedAmount e currentValue - são campos DIFERENTES
   - Se houver apenas um valor disponível, use-o para currentValue e deixe investedAmount como null

4. TICKERS/CÓDIGOS:
   - Brasil: tickers terminam em números (PETR4, VALE3, BOVA11)
   - EUA: sem sufixos numéricos (AAPL, GOOGL, TSLA)
   - Offshore: pode ter variações (ADRs, etc)

4. CATEGORIZAÇÃO DE ATIVOS (4 categorias principais):
   
   RENDA VARIÁVEL (variableIncome):
   - Stocks/Ações: PETR4, VALE3, AAPL, GOOGL
   - ETFs: BOVA11, IVVB11, SPY, VOO
   - ADRs/BDRs: VALE, PBR, A1MD34
   - Opções e Futuros
   
   RENDA FIXA (fixedIncome) - REGRA ASSERTIVA:
   - QUALQUER ativo sob seção/categoria Fixed Income, US Fixed Income, Non-US Fixed Income, Global Fixed Income DEVE ir para fixedIncome
   - Brasil: LCI, LCA, CDB, Debêntures, CRI, CRA, Tesouro Direto
   - Internacional: Treasury Bonds, Corporate Bonds, Municipal Bonds
   - Offshore: Government Bonds, High-Yield Bonds
   - Money Market Funds classificados como Fixed Income no statement
   - INCLUA TODOS os ativos listados sob qualquer subcategoria de Fixed Income, independente do tipo
   
   ATIVOS ALTERNATIVOS (alternativeAssets):
   - REITs/FIIs: Fundos imobiliários (brasileiros e internacionais)
   - Private Equity: Fundos de participações, venture capital
   - Hedge Funds: Fundos multimercado, long/short, macro
   - Cryptocurrency: Bitcoin, Ethereum, stablecoins, tokens
   - Commodities: Ouro, Prata, Petróleo, contratos futuros
   - Structured Products: Notas estruturadas, COEs
   - Real Assets: Infraestrutura, timóvel direto, florestal
   - Art & Collectibles: Arte, vinículos, ativos tangíveis
   - FLEXIBILIDADE: Qualquer ativo que NÃO se encaixe em renda variável, fixa ou cash
   - Use o campo type de forma descritiva e o additionalData para dados específicos
   
   CASH (cash):
   - Contas correntes e poupança
   - Money Market Funds
   - Sweep Accounts (varrição automática)
   - CDs de curtissimo prazo
   - Saldo disponível para saque

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

8. PROCESSO OBRIGATÓRIO DE CATEGORIZAÇÃO EM 2 ETAPAS:

   ETAPA 1 - IDENTIFICAÇÃO DE SEÇÕES:
   - Antes de categorizar QUALQUER ativo, identifique TODAS as seções do documento
   - Procure por cabeçalhos: ""Cash"", ""Fixed Income"", ""US Fixed Income"", ""Non-US Fixed Income"", ""Global Fixed Income"", ""Equity"", etc
   - Mapeie cada ativo para a seção onde ele aparece fisicamente no documento
   - DOCUMENTE no reasoning.specificDecisions qual seção cada ativo pertence
   
   ETAPA 2 - CATEGORIZAÇÃO BASEADA EXCLUSIVAMENTE NA SEÇÃO:
   - Use APENAS a seção identificada na Etapa 1 para categorizar
   - IGNORE completamente o tipo do ativo (Money Market Fund, Corporate Bond, etc)
   - Regras absolutas:
     * Ativo sob cabeçalho ""US Fixed Income"" → fixedIncome.assets[]
     * Ativo sob cabeçalho ""Non-US Fixed Income"" → fixedIncome.assets[]
     * Ativo sob cabeçalho ""Global Fixed Income"" → fixedIncome.assets[]
     * Ativo sob cabeçalho ""Fixed Income"" → fixedIncome.assets[]
     * Ativo sob cabeçalho ""Cash"" → cash.positions[]
     * Ativo sob cabeçalho ""Equity"" → variableIncome.stocks[]

9. EXEMPLO PRÁTICO (SIGA RIGOROSAMENTE):
   - Documento mostra: ""Global Fixed Income"" como cabeçalho, depois ""ICS USD LIQ-PRM ACC""
   - Ação OBRIGATÓRIA: classificar ICS como fixedIncome.assets[]
   - Ação PROIBIDA: classificar como cash só porque é Money Market Fund
   - Reasoning: ""ICS encontrado sob seção Global Fixed Income, portanto classificado como fixedIncome""

10. AGREGAÇÃO OBRIGATÓRIA:
   - TODAS as subcategorias de Fixed Income devem ser AGREGADAS em um único array fixedIncome.assets[]
   - Conte TODOS os ativos: se documento mostra 7 bonds/MMFs em Fixed Income, retorne 7 itens
   - NUNCA deixe ativos de fora por estarem em subcategorias geográficas

11. CASH (somente ativos FORA de qualquer seção Fixed Income):
   - Funcione para QUALQUER formato: PDF de corretora, banco, offshore, consolidado
   - Ignore cabeçalhos, rodapés, informações irrelevantes
   - Foque apenas nos dados de investimentos

12. REASONING (RACIOCÍNIO - OBRIGATÓRIO):
   - documentAnalysis: Descreva o tipo de documento detectado e sua estrutura geral
   - currencyDetection: Explique como identificou a moeda (símbolos, termos encontrados)
   - categoryDecisions: 
     * PRIMEIRO: Liste TODAS as seções identificadas no documento (ex: ""Seções encontradas: Cash, US Fixed Income, Non-US Fixed Income, Global Fixed Income, Equity"")
     * SEGUNDO: Para CADA ativo, documente qual seção ele pertence e por que foi categorizado assim
     * TERCEIRO: Contagem total por categoria (ex: ""7 ativos em fixedIncome: 5 de US Fixed Income + 1 de Non-US + 1 de Global"")
   - uncertainties: Liste qualquer ambiguidade ou decisão difícil (ex: ativo poderia ser X ou Y)
   - assumptions: Liste premissas assumidas (ex: assumido USD por aparecer $ sem contexto)
   - dataQualityAssessment: Avalie a qualidade geral (tabelas estruturadas vs texto livre, confiança geral)
   - specificDecisions: Documente CADA decisão de categorização com formato:
     * ""[Nome do Ativo]: Encontrado sob seção [Nome da Seção] → Categorizado como [Categoria]""
     * Exemplo: ""ICS USD LIQ-PRM ACC: Encontrado sob seção Global Fixed Income → Categorizado como fixedIncome""

Seja preciso, consistente e adaptável. A qualidade dos dados é crítica.
DOCUMENTE SEU RACIOCÍNIO - isso é fundamental para auditoria e melhoria contínua.";
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
        public VariableIncomePortfolio VariableIncome { get; set; } = new();
        public FixedIncomePortfolio FixedIncome { get; set; } = new();
        public AlternativeAssetsPortfolio AlternativeAssets { get; set; } = new();
        public CashPortfolio Cash { get; set; } = new();
        public AiReasoning? Reasoning { get; set; }
        
        // DEPRECATED: Manter para compatibilidade temporária
        public StockPortfolio? Stocks { get; set; }
    }
}
