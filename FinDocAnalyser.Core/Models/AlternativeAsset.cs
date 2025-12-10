using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FinDocAnalyzer.Core.Models;

/// <summary>
/// Portfolio de Ativos Alternativos (REITs, FIIs, Private Equity, Hedge Funds, Crypto, Commodities)
/// </summary>
public class AlternativeAssetsPortfolio
{
    public decimal TotalInvested { get; set; }
    public string Currency { get; set; } = "USD";
    public List<AlternativeAsset> Assets { get; set; } = new();
}

/// <summary>
/// Ativo Alternativo individual - modelo flexível para diversos tipos de ativos não convencionais
/// </summary>
public class AlternativeAsset
{
    /// <summary>
    /// Nome do ativo/fundo/investimento
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Tipo/categoria do ativo (ex: "REIT", "FII", "Private Equity", "Hedge Fund", "Crypto", "Commodity", "Structured Product", "Art", "Venture Capital", etc)
    /// Campo FLEXÍVEL - aceita qualquer classificação
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Símbolo/ticker/identificador se houver (ex: "BTC-USD", "O", "HGLG11")
    /// OPCIONAL - nem todos ativos alternativos têm ticker
    /// </summary>
    public string? Symbol { get; set; }

    /// <summary>
    /// Emissor/gestor/administrador do fundo ou ativo
    /// OPCIONAL - pode não estar disponível
    /// </summary>
    public string? Issuer { get; set; }

    /// <summary>
    /// Descrição adicional ou estratégia (para fundos complexos)
    /// OPCIONAL - campo livre para informações extras
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Quantidade de cotas/unidades/tokens
    /// OPCIONAL - nem todos ativos têm unidades quantificáveis
    /// </summary>
    public decimal? Quantity { get; set; }

    /// <summary>
    /// Preço/valor unitário (quando aplicável)
    /// OPCIONAL - para ativos com preço por unidade
    /// </summary>
    public decimal? UnitPrice { get; set; }

    /// <summary>
    /// Valor investido/capital comprometido
    /// Principal campo financeiro - sempre tentar extrair
    /// </summary>
    public decimal InvestedAmount { get; set; }

    /// <summary>
    /// Valor atual estimado/NAV/market value
    /// </summary>
    public decimal CurrentValue { get; set; }

    /// <summary>
    /// Retorno absoluto (lucro/prejuízo)
    /// </summary>
    public decimal? Return { get; set; }

    /// <summary>
    /// Retorno percentual
    /// </summary>
    public decimal? ReturnPercentage { get; set; }

    /// <summary>
    /// Yield/rendimento/distribuições (formato livre: "5.2%", "$120/month", etc)
    /// OPCIONAL - para ativos que geram renda periódica
    /// </summary>
    public string? Yield { get; set; }

    /// <summary>
    /// Taxa de administração ou management fee
    /// OPCIONAL - importante para fundos
    /// </summary>
    public string? ManagementFee { get; set; }

    /// <summary>
    /// Período de lock-up ou carencia
    /// OPCIONAL - para investimentos com restrição de liquidez
    /// </summary>
    public string? LockupPeriod { get; set; }

    /// <summary>
    /// Data de início/subscrição/aquisição
    /// </summary>
    public DateTime? InceptionDate { get; set; }

    /// <summary>
    /// Data de vencimento/exit/liquidação (se aplicável)
    /// </summary>
    public DateTime? MaturityDate { get; set; }

    /// <summary>
    /// Dados adicionais em formato chave-valor para flexibilidade máxima
    /// Permite capturar campos específicos de cada tipo de ativo
    /// Ex: {"Vintage": "2023", "Geography": "Global", "Strategy": "Long/Short Equity"}
    /// </summary>
    public Dictionary<string, string>? AdditionalData { get; set; }

    /// <summary>
    /// Confiança da extração (0.0 a 1.0)
    /// </summary>
    public decimal Confidence { get; set; }

    /// <summary>
    /// Explicação do nível de confiança
    /// </summary>
    public string ConfidenceReason { get; set; } = string.Empty;
}
