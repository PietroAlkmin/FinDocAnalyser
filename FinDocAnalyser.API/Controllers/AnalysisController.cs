using FinDocAnalyzer.Core.Models;
using FinDocAnalyzer.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace FinDocAnalyzer.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AnalysisController : ControllerBase
{
    private readonly AnalysisOrchestrator _orchestrator;
    private readonly ILogger<AnalysisController> _logger;
    private const long MaxFileSizeBytes = 50 * 1024 * 1024; // 50 MB

    public AnalysisController(
        AnalysisOrchestrator orchestrator,
        ILogger<AnalysisController> logger)
    {
        _orchestrator = orchestrator;
        _logger = logger;
    }

    /// <summary>
    /// Upload e análise de PDF financeiro
    /// </summary>
    /// <param name="file">Arquivo PDF do relatório financeiro</param>
    /// <returns>ID da análise para consulta posterior</returns>
    [HttpPost]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(AnalysisResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> AnalyzePdf(IFormFile file)
    {
        try
        {
            // Validação 1: Arquivo enviado?
            if (file == null || file.Length == 0)
            {
                return BadRequest(new ErrorResponse
                {
                    Error = "Nenhum arquivo foi enviado",
                    Details = "Por favor, envie um arquivo PDF válido"
                });
            }

            // Validação 2: Tamanho do arquivo
            if (file.Length > MaxFileSizeBytes)
            {
                return BadRequest(new ErrorResponse
                {
                    Error = "Arquivo muito grande",
                    Details = $"O tamanho máximo permitido é {MaxFileSizeBytes / 1024 / 1024} MB"
                });
            }

            // Validação 3: Tipo de arquivo
            if (!file.ContentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new ErrorResponse
                {
                    Error = "Tipo de arquivo inválido",
                    Details = "Apenas arquivos PDF são aceitos"
                });
            }

            _logger.LogInformation("Iniciando análise do arquivo: {FileName} ({FileSize} bytes)",
                file.FileName, file.Length);

            // Lê o arquivo para byte array
            byte[] fileContent;
            using (var memoryStream = new MemoryStream())
            {
                await file.CopyToAsync(memoryStream);
                fileContent = memoryStream.ToArray();
            }

            // Processa o PDF
            var analysisId = await _orchestrator.ProcessPdfAsync(fileContent, file.FileName);

            _logger.LogInformation("Análise concluída com sucesso. AnalysisId: {AnalysisId}", analysisId);

            // Retorna 202 Accepted com o ID
            var response = new AnalysisResponse
            {
                AnalysisId = analysisId,
                Message = "Análise concluída com sucesso",
                Endpoints = new AnalysisEndpoints
                {
                    Total = Url.Action(nameof(GetTotal), new { id = analysisId })!,
                    Classification = Url.Action(nameof(GetClassification), new { id = analysisId })!,
                    Stocks = Url.Action(nameof(GetStocks), new { id = analysisId })!,
                    FixedIncome = Url.Action(nameof(GetFixedIncome), new { id = analysisId })!
                }
            };

            return Accepted(response);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Erro de validação ao processar arquivo");
            return BadRequest(new ErrorResponse
            {
                Error = "Erro ao processar arquivo",
                Details = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro inesperado ao processar arquivo");
            return StatusCode(500, new ErrorResponse
            {
                Error = "Erro interno do servidor",
                Details = "Ocorreu um erro ao processar sua solicitação. Por favor, tente novamente."
            });
        }
    }

    /// <summary>
    /// Obtém o total investido
    /// </summary>
    [HttpGet("{id}/total")]
    [ProducesResponseType(typeof(TotalInvested), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTotal(Guid id)
    {
        var result = await _orchestrator.GetTotalAsync(id);

        if (result == null)
        {
            return NotFound(new ErrorResponse
            {
                Error = "Análise não encontrada",
                Details = "A análise não existe ou já expirou (resultados disponíveis por 30 minutos)"
            });
        }

        return Ok(result);
    }

    /// <summary>
    /// Obtém a classificação de ativos
    /// </summary>
    [HttpGet("{id}/classification")]
    [ProducesResponseType(typeof(AssetClassification), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetClassification(Guid id)
    {
        var result = await _orchestrator.GetClassificationAsync(id);

        if (result == null)
        {
            return NotFound(new ErrorResponse
            {
                Error = "Análise não encontrada",
                Details = "A análise não existe ou já expirou (resultados disponíveis por 30 minutos)"
            });
        }

        return Ok(result);
    }

    /// <summary>
    /// Obtém as ações
    /// </summary>
    [HttpGet("{id}/stocks")]
    [ProducesResponseType(typeof(StockPortfolio), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStocks(Guid id)
    {
        var result = await _orchestrator.GetStocksAsync(id);

        if (result == null)
        {
            return NotFound(new ErrorResponse
            {
                Error = "Análise não encontrada",
                Details = "A análise não existe ou já expirou (resultados disponíveis por 30 minutos)"
            });
        }

        return Ok(result);
    }

    /// <summary>
    /// Obtém a renda fixa
    /// </summary>
    [HttpGet("{id}/fixed-income")]
    [ProducesResponseType(typeof(FixedIncomePortfolio), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetFixedIncome(Guid id)
    {
        var result = await _orchestrator.GetFixedIncomeAsync(id);

        if (result == null)
        {
            return NotFound(new ErrorResponse
            {
                Error = "Análise não encontrada",
                Details = "A análise não existe ou já expirou (resultados disponíveis por 30 minutos)"
            });
        }

        return Ok(result);
    }

    /// <summary>
    /// Obtém o resultado completo da análise
    /// </summary>
    [HttpGet("{id}/complete")]
    [ProducesResponseType(typeof(AnalysisResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetComplete(Guid id)
    {
        var result = await _orchestrator.GetAnalysisAsync(id);

        if (result == null)
        {
            return NotFound(new ErrorResponse
            {
                Error = "Análise não encontrada",
                Details = "A análise não existe ou já expirou (resultados disponíveis por 30 minutos)"
            });
        }

        return Ok(result);
    }
}

// DTOs para respostas
public class AnalysisResponse
{
    public Guid AnalysisId { get; set; }
    public string Message { get; set; } = string.Empty;
    public AnalysisEndpoints Endpoints { get; set; } = new();
}

public class AnalysisEndpoints
{
    public string Total { get; set; } = string.Empty;
    public string Classification { get; set; } = string.Empty;
    public string Stocks { get; set; } = string.Empty;
    public string FixedIncome { get; set; } = string.Empty;
}

public class ErrorResponse
{
    public string Error { get; set; } = string.Empty;
    public string? Details { get; set; }
}