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
            throw new InvalidOperationException($"Error analyzing document with AI: {ex.Message}", ex);
        }
    }

    private string CreateSystemPrompt()
    {
        return CreateUniversalPrompt();
    }

    private string CreateUniversalPrompt()
    {
        return @"You are a GLOBAL expert in analyzing financial reports (Brazilian, international, offshore). 

Your task is to extract structured data from ANY type of investment report, regardless of format, institution, or country.

IMPORTANT: Return ONLY a valid JSON object, with no additional text before or after.

The JSON must follow EXACTLY this structure:

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
        ""assetClassName"": string (e.g.: ""Variable Income"", ""Fixed Income"", ""Alternative Assets"", ""Cash""),
        ""invested"": number,
        ""percentage"": number,
        ""confidence"": number (0.0 to 1.0),
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
        ""return"": number or null,
        ""returnPercentage"": number or null,
        ""yield"": string or null,
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
        ""return"": number or null,
        ""returnPercentage"": number or null,
        ""rate"": string or null,
        ""yield"": string or null,
        ""maturityDate"": ""YYYY-MM-DD"" or null,
        ""applicationDate"": ""YYYY-MM-DD"" or null,
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
        ""type"": string (FLEXIBLE: ""REIT"", ""FII"", ""PrivateEquity"", ""HedgeFund"", ""Cryptocurrency"", ""Commodity"", ""StructuredProduct"", ""Art"", ""VentureCapital"", ""RealAssets"", ""Infrastructure"", or ANY other category),
        ""symbol"": string or null (ticker/identifier if available),
        ""issuer"": string or null (manager/administrator),
        ""description"": string or null (additional description, strategy),
        ""quantity"": number or null (shares/units if applicable),
        ""unitPrice"": number or null (unit price if applicable),
        ""investedAmount"": number,
        ""currentValue"": number,
        ""return"": number or null,
        ""returnPercentage"": number or null,
        ""yield"": string or null (yield/distributions),
        ""managementFee"": string or null (management fee),
        ""lockupPeriod"": string or null (lock-up/grace period),
        ""inceptionDate"": ""YYYY-MM-DD"" or null,
        ""maturityDate"": ""YYYY-MM-DD"" or null,
        ""additionalData"": object or null (key-value with specific extra data),
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
        ""yield"": string or null,
        ""currency"": string,
        ""isAvailable"": boolean,
        ""confidence"": number,
        ""confidenceReason"": string
      }
    ]
  },
  ""reasoning"": {
    ""documentAnalysis"": string (initial analysis of document type and structure),
    ""currencyDetection"": string (how the main currency was detected),
    ""categoryDecisions"": string (main categorization decisions),
    ""uncertainties"": [string] (points of uncertainty or ambiguity),
    ""assumptions"": [string] (assumptions made during analysis),
    ""dataQualityAssessment"": string (assessment of extracted data quality),
    ""specificDecisions"": {
      ""key"": string (important specific decisions, e.g.: why X was classified as Y)
    }
  }
}

UNIVERSAL RULES for extraction:

1. AUTOMATIC DETECTION:
   - Automatically detect the report currency (R$, $, €, £, USD, BRL, etc)
   - Identify if it's Brazilian (B3, Bovespa) or international (NYSE, NASDAQ, etc)
   - Recognize different date formats and convert to ISO (YYYY-MM-DD)

2. NUMERIC VALUES:
   - Extract WITHOUT currency symbols or thousand separators
   - Always use dot (.) as decimal separator
   - For percentages, use decimal value (e.g.: 42.6 for 42.6%)

3. CRITICAL DISTINCTION - INVESTED VALUE vs CURRENT VALUE:
   - investedAmount/averagePrice: Original investment cost (common terms: ""Original Cost"", ""Adjusted Cost"", ""Cost Basis"", ""Custo de Aquisição"", ""Valor Aplicado"", ""Invested"", ""Purchase Price"")
   - currentValue: Current market value (common terms: ""Market Value"", ""Current Value"", ""Value"", ""Valor Atual"", ""Posição"", ""Position Value"")
   - return: Difference between currentValue and investedAmount (common terms: ""Gain/Loss"", ""Unrealized Gain/Loss"", ""Profit/Loss"", ""Lucro/Prejuízo"", ""Rentabilidade Absoluta"")
   - returnPercentage: (return / investedAmount) * 100
   - NEVER use the same value for investedAmount and currentValue - they are DIFFERENT fields
   - If only one value is available, use it for currentValue and leave investedAmount as null

4. TICKERS/CODES:
   - Brazil: tickers end in numbers (PETR4, VALE3, BOVA11)
   - USA: no numeric suffixes (AAPL, GOOGL, TSLA)
   - Offshore: may have variations (ADRs, etc)

4. ASSET CATEGORIZATION (4 main categories):
   
   VARIABLE INCOME (variableIncome):
   - Stocks: PETR4, VALE3, AAPL, GOOGL
   - ETFs: BOVA11, IVVB11, SPY, VOO
   - ADRs/BDRs: VALE, PBR, A1MD34
   - Options and Futures
   
   FIXED INCOME (fixedIncome) - ASSERTIVE RULE:
   - ANY asset under section/category Fixed Income, US Fixed Income, Non-US Fixed Income, Global Fixed Income MUST go to fixedIncome
   - Brazil: LCI, LCA, CDB, Debentures, CRI, CRA, Treasury Direct
   - International: Treasury Bonds, Corporate Bonds, Municipal Bonds
   - Offshore: Government Bonds, High-Yield Bonds
   - Money Market Funds classified as Fixed Income in the statement
   - INCLUDE ALL assets listed under any Fixed Income subcategory, regardless of type
   
   ALTERNATIVE ASSETS (alternativeAssets):
   - REITs/FIIs: Real estate funds (Brazilian and international)
   - Private Equity: Participation funds, venture capital
   - Hedge Funds: Multi-market funds, long/short, macro
   - Cryptocurrency: Bitcoin, Ethereum, stablecoins, tokens
   - Commodities: Gold, Silver, Oil, futures contracts
   - Structured Products: Structured notes, COEs
   - Real Assets: Infrastructure, direct real estate, forestry
   - Art & Collectibles: Art, wines, tangible assets
   - FLEXIBILITY: Any asset that does NOT fit in variable income, fixed income or cash
   - Use the type field descriptively and additionalData for specific data
   
   CASH (cash):
   - Checking and savings accounts
   - Money Market Funds
   - Sweep Accounts (automatic sweep)
   - Very short-term CDs
   - Available balance for withdrawal

5. CONFIDENCE SCORES (seja rigoroso):
   - 0.95-1.0: Dados em tabelas estruturadas com labels explícitos
   - 0.85-0.94: Dados claramente identificáveis mas requerem interpretação
   - 0.70-0.84: Dados inferidos do contexto geral
   - 0.50-0.69: Uncertain or estimated data
   - < 0.50: DO NOT INCLUDE - use null

6. CONFIDENCE REASON:
   - Explain BRIEFLY why you assigned this confidence
   - E.g.: ""Value in structured table"", ""Inferred from total balance"", ""Ticker identified in header""

7. MISSING DATA:
   - If data is not available, use null
   - DO NOT invent values
   - Empty arrays [] if there is no data for the category

8. MANDATORY 2-STEP CATEGORIZATION PROCESS:

   STEP 1 - SECTION IDENTIFICATION:
   - Before categorizing ANY asset, identify ALL sections of the document
   - Look for headers: ""Cash"", ""Fixed Income"", ""US Fixed Income"", ""Non-US Fixed Income"", ""Global Fixed Income"", ""Equity"", etc
   - Map each asset to the section where it physically appears in the document
   - DOCUMENT in reasoning.specificDecisions which section each asset belongs to
   
   STEP 2 - CATEGORIZATION BASED EXCLUSIVELY ON SECTION:
   - Use ONLY the section identified in Step 1 to categorize
   - COMPLETELY IGNORE the asset type (Money Market Fund, Corporate Bond, etc)
   - Absolute rules:
     * Asset under ""US Fixed Income"" header → fixedIncome.assets[]
     * Asset under ""Non-US Fixed Income"" header → fixedIncome.assets[]
     * Asset under ""Global Fixed Income"" header → fixedIncome.assets[]
     * Asset under ""Fixed Income"" header → fixedIncome.assets[]
     * Asset under ""Cash"" header → cash.positions[]
     * Asset under ""Equity"" header → variableIncome.stocks[]

9. PRACTICAL EXAMPLE (FOLLOW STRICTLY):
   - Document shows: ""Global Fixed Income"" as header, then ""ICS USD LIQ-PRM ACC""
   - MANDATORY ACTION: classify ICS as fixedIncome.assets[]
   - PROHIBITED ACTION: classify as cash just because it's a Money Market Fund
   - Reasoning: ""ICS found under Global Fixed Income section, therefore classified as fixedIncome""

10. MANDATORY AGGREGATION:
   - ALL Fixed Income subcategories must be AGGREGATED into a single fixedIncome.assets[] array
   - Count ALL assets: if document shows 7 bonds/MMFs in Fixed Income, return 7 items
   - NEVER leave assets out because they are in geographic subcategories

11. CASH (only assets OUTSIDE any Fixed Income section):
   - Work for ANY format: brokerage PDF, bank, offshore, consolidated
   - Ignore headers, footers, irrelevant information
   - Focus only on investment data

12. REASONING (MANDATORY):
   - documentAnalysis: Describe the detected document type and its overall structure
   - currencyDetection: Explain how you identified the currency (symbols, terms found)
   - categoryDecisions: 
     * FIRST: List ALL sections identified in the document (e.g.: ""Sections found: Cash, US Fixed Income, Non-US Fixed Income, Global Fixed Income, Equity"")
     * SECOND: For EACH asset, document which section it belongs to and why it was categorized that way
     * THIRD: Total count by category (e.g.: ""7 assets in fixedIncome: 5 from US Fixed Income + 1 from Non-US + 1 from Global"")
   - uncertainties: List any ambiguity or difficult decision (e.g.: asset could be X or Y)
   - assumptions: List assumptions made (e.g.: assumed USD because $ appeared without context)
   - dataQualityAssessment: Evaluate overall quality (structured tables vs free text, general confidence)
   - specificDecisions: Document EACH categorization decision with format:
     * ""[Asset Name]: Found under [Section Name] section → Categorized as [Category]""
     * Example: ""ICS USD LIQ-PRM ACC: Found under Global Fixed Income section → Categorized as fixedIncome""

Be precise, consistent and adaptable. Data quality is critical.
DOCUMENT YOUR REASONING - this is essential for audit and continuous improvement.";
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
                throw new InvalidOperationException("AI returned empty or invalid response");
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
