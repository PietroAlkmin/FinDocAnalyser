using System.Text.Json;
using FinDocAnalyzer.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace FinDocAnalyzer.Infrastructure.Currency;

/// <summary>
/// Conversor USD↔BRL usando API oficial do Banco Central do Brasil
/// APENAS suporta conversão entre dólar e real
/// </summary>
public class BcbUsdBrlConverter : ICurrencyConverter
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<BcbUsdBrlConverter> _logger;
    private const string BcbBaseUrl = "https://olinda.bcb.gov.br/olinda/servico/PTAX/versao/v1/odata";

    // Cache de cotações (evita múltiplas chamadas para a mesma data)
    private static readonly Dictionary<string, (decimal rate, DateTime cached)> _rateCache = new();
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(6);

    public BcbUsdBrlConverter(
        HttpClient httpClient,
        ILogger<BcbUsdBrlConverter> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<decimal> GetExchangeRateAsync(string from, string to, DateTime? date = null)
    {
        // Normaliza moedas
        from = from.ToUpperInvariant();
        to = to.ToUpperInvariant();

        // Se ambas as moedas são iguais, taxa = 1
        if (from == to)
            return 1.0m;

        // Valida que é USD↔BRL
        if (!IsValidPair(from, to))
        {
            throw new NotSupportedException(
                $"Conversão {from}→{to} não suportada. " +
                $"Este conversor suporta APENAS USD↔BRL.");
        }

        var targetDate = date ?? DateTime.UtcNow.AddHours(-3); // Hora de Brasília

        // Verifica cache
        var cacheKey = $"USD_BRL_{targetDate:yyyy-MM-dd}";
        if (_rateCache.TryGetValue(cacheKey, out var cached))
        {
            if (DateTime.UtcNow - cached.cached < CacheDuration)
            {
                _logger.LogDebug("[BCB] Cache hit: USD→BRL = {Rate:N4} (data: {Date:yyyy-MM-dd})", 
                    cached.rate, targetDate);
                
                // Se pediu BRL→USD, inverte a taxa
                return to == "BRL" ? cached.rate : 1 / cached.rate;
            }
        }

        // Busca cotação no BCB
        var usdToBrlRate = await FetchUsdBrlRateAsync(targetDate);

        // Armazena no cache
        _rateCache[cacheKey] = (usdToBrlRate, DateTime.UtcNow);

        // Retorna taxa correta dependendo da direção
        var rate = to == "BRL" ? usdToBrlRate : 1 / usdToBrlRate;

        _logger.LogInformation("[BCB] {From}→{To} = {Rate:N4} (data: {Date:yyyy-MM-dd})",
            from, to, rate, targetDate);

        return rate;
    }

    public async Task<decimal> ConvertAsync(decimal amount, string from, string to, DateTime? date = null)
    {
        var rate = await GetExchangeRateAsync(from, to, date);
        var converted = amount * rate;

        _logger.LogInformation("[BCB] Conversão: {Amount:N2} {From} → {Converted:N2} {To} (taxa: {Rate:N4})",
            amount, from, converted, to, rate);

        return converted;
    }

    /// <summary>
    /// Busca a cotação USD→BRL no Banco Central do Brasil
    /// </summary>
    private async Task<decimal> FetchUsdBrlRateAsync(DateTime date, int retryCount = 0)
    {
        const int MaxRetries = 10; // Máximo 10 dias para trás (fins de semana + feriados)

        if (retryCount >= MaxRetries)
        {
            throw new InvalidOperationException(
                $"Não foi possível obter cotação USD→BRL após {MaxRetries} tentativas. " +
                $"Última data tentada: {date:yyyy-MM-dd}");
        }

        try
        {
            // Formata data no padrão BCB: MM-dd-yyyy
            var dateStr = date.ToString("MM-dd-yyyy");

            // Endpoint específico do dólar
            var url = $"{BcbBaseUrl}/CotacaoDolarDia(dataCotacao=@dataCotacao)" +
                     $"?@dataCotacao='{dateStr}'&$format=json";

            _logger.LogDebug("[BCB] Consultando USD→BRL em {Date} (tentativa {Retry})", 
                dateStr, retryCount + 1);

            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            
            var result = JsonSerializer.Deserialize<BcbDolarResponse>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (result?.Value == null || result.Value.Count == 0)
            {
                // Tenta dia anterior (BCB pode não ter cotação em feriados/fins de semana)
                _logger.LogWarning("[BCB] Sem cotação para {Date}, tentando dia anterior", dateStr);
                return await FetchUsdBrlRateAsync(date.AddDays(-1), retryCount + 1);
            }

            var cotacao = result.Value[0];
            var rate = cotacao.CotacaoVenda ?? cotacao.CotacaoCompra ?? 0;

            if (rate == 0)
            {
                throw new InvalidOperationException($"Cotação inválida para USD em {dateStr}");
            }

            _logger.LogInformation("[BCB] ✅ Cotação USD→BRL obtida: {Rate:N4} (data: {Date}, {Time})",
                rate, dateStr, cotacao.DataHoraCotacao);

            return rate;
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            _logger.LogError(ex, "[BCB] Erro ao buscar cotação USD em {Date}", date);
            
            // Tenta dia anterior
            if (retryCount < MaxRetries - 1)
            {
                _logger.LogWarning("[BCB] Tentando dia anterior devido a erro");
                return await FetchUsdBrlRateAsync(date.AddDays(-1), retryCount + 1);
            }
            
            throw new InvalidOperationException(
                $"Não foi possível obter cotação USD→BRL no Banco Central: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Valida se o par de moedas é suportado (apenas USD↔BRL)
    /// </summary>
    private bool IsValidPair(string from, string to)
    {
        var validCombinations = new[]
        {
            ("USD", "BRL"),
            ("BRL", "USD")
        };

        return validCombinations.Any(pair => 
            (pair.Item1 == from && pair.Item2 == to) ||
            (pair.Item1 == to && pair.Item2 == from));
    }
}

// DTO para deserialização da resposta do BCB
internal class BcbDolarResponse
{
    public List<BcbDolarCotacao> Value { get; set; } = new();
}

internal class BcbDolarCotacao
{
    public decimal? CotacaoCompra { get; set; }
    public decimal? CotacaoVenda { get; set; }
    public string DataHoraCotacao { get; set; } = string.Empty;
}
