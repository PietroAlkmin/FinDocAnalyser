namespace FinDocAnalyzer.Core.Interfaces;

/// <summary>
/// Interface para conversão de moedas com cotações reais
/// </summary>
public interface ICurrencyConverter
{
    /// <summary>
    /// Obtém a taxa de câmbio entre duas moedas
    /// </summary>
    /// <param name="from">Moeda de origem (ex: "USD")</param>
    /// <param name="to">Moeda de destino (ex: "BRL")</param>
    /// <param name="date">Data da cotação (null = hoje)</param>
    /// <returns>Taxa de conversão</returns>
    Task<decimal> GetExchangeRateAsync(string from, string to, DateTime? date = null);

    /// <summary>
    /// Converte um valor de uma moeda para outra
    /// </summary>
    /// <param name="amount">Valor a converter</param>
    /// <param name="from">Moeda de origem</param>
    /// <param name="to">Moeda de destino</param>
    /// <param name="date">Data da cotação (null = hoje)</param>
    /// <returns>Valor convertido</returns>
    Task<decimal> ConvertAsync(decimal amount, string from, string to, DateTime? date = null);
}
