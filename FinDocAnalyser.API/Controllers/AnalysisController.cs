using FinDocAnalyzer.Core.Models;
using FinDocAnalyzer.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace FinDocAnalyzer.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AnalysisController : ControllerBase
{
    private readonly AnalysisOrchestrator _orchestrator;
    private readonly ConsolidationService _consolidationService;
    private readonly ILogger<AnalysisController> _logger;
    private const long MaxFileSizeBytes = 50 * 1024 * 1024; // 50 MB

    public AnalysisController(
        AnalysisOrchestrator orchestrator,
        ConsolidationService consolidationService,
        ILogger<AnalysisController> logger)
    {
        _orchestrator = orchestrator;
        _consolidationService = consolidationService;
        _logger = logger;
    }

    /// <summary>
    /// Upload e análise de PDF(s) financeiro(s) - gera análise consolidada da carteira
    /// </summary>
    /// <param name="files">Arquivo(s) PDF do(s) relatório(s) financeiro(s) - máximo 20 arquivos</param>
    /// <param name="period">Período dos relatórios (opcional, ex: "Setembro 2024")</param>
    /// <returns>ID da análise consolidada para consulta posterior</returns>
    [HttpPost]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ConsolidatedAnalysisResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> AnalyzePdfs(List<IFormFile> files, [FromForm] string? period = null)
    {
        try
        {
            // Validação 1: Arquivos enviados?
            if (files == null || files.Count == 0)
            {
                return BadRequest(new ErrorResponse
                {
                    Error = "Nenhum arquivo foi enviado",
                    Details = "Por favor, envie pelo menos um arquivo PDF válido"
                });
            }

            // Validação 2: Limite de arquivos
            if (files.Count > 20)
            {
                return BadRequest(new ErrorResponse
                {
                    Error = "Muitos arquivos",
                    Details = "Máximo de 20 arquivos por requisição"
                });
            }

            _logger.LogInformation("Iniciando análise de carteira: {FileCount} arquivos | Período: {Period}",
                files.Count, period ?? "não especificado");

            var tasks = new List<Task<BatchFileResult>>();

            foreach (var file in files)
            {
                // Processa cada PDF individualmente (async)
                var task = ProcessSingleFileAsync(file);
                tasks.Add(task);
            }

            // Processa todos os PDFs em paralelo (uma AI por PDF)
            var results = await Task.WhenAll(tasks);

            var successCount = results.Count(r => r.Success);
            var failureCount = results.Count(r => !r.Success);

            // Filtra apenas sucessos
            var successfulResults = results.Where(r => r.Success && r.AnalysisId.HasValue).ToList();

            if (successfulResults.Count == 0)
            {
                return BadRequest(new ErrorResponse
                {
                    Error = "Nenhum arquivo foi processado com sucesso",
                    Details = string.Join("; ", results.Where(r => !r.Success).Select(r => r.Error))
                });
            }

            // Consolida automaticamente
            var analysisIds = successfulResults.Select(r => r.AnalysisId!.Value).ToList();
            var consolidated = await _consolidationService.ConsolidateAsync(analysisIds, period ?? string.Empty);

            _logger.LogInformation(
                "Análise concluída: {ConsolidationId} | {SuccessCount}/{TotalCount} arquivos | Total: {Total:C}",
                consolidated.ConsolidationId, successCount, files.Count, consolidated.Summary.TotalInvested);

            // Retorna 202 Accepted com o ID consolidado
            var response = new ConsolidatedAnalysisResponse
            {
                ConsolidationId = consolidated.ConsolidationId,
                TotalFiles = files.Count,
                SuccessCount = successCount,
                FailureCount = failureCount,
                Message = $"Carteira analisada com sucesso: {successCount} de {files.Count} arquivos processados",
                Summary = new PortfolioSummaryDto
                {
                    TotalInvested = consolidated.Summary.TotalInvested,
                    TotalAssets = consolidated.Summary.TotalAssets,
                    Currency = consolidated.Summary.Currency
                },
                Endpoints = new ConsolidatedEndpoints
                {
                    Full = Url.Action(nameof(GetConsolidated), new { id = consolidated.ConsolidationId })!,
                    Summary = Url.Action(nameof(GetConsolidatedSummary), new { id = consolidated.ConsolidationId })!,
                    Performance = Url.Action(nameof(GetConsolidatedPerformance), new { id = consolidated.ConsolidationId })!,
                    Liquidity = Url.Action(nameof(GetConsolidatedLiquidity), new { id = consolidated.ConsolidationId })!
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
    /// Obtém análise consolidada completa da carteira
    /// </summary>
    /// <param name="id">ID da consolidação</param>
    [HttpGet("consolidated/{id}")]
    [ProducesResponseType(typeof(ConsolidatedPortfolio), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetConsolidated(Guid id)
    {
        var result = await _consolidationService.GetConsolidatedAsync(id);

        if (result == null)
        {
            return NotFound(new ErrorResponse
            {
                Error = "Análise consolidada não encontrada",
                Details = "A consolidação não existe ou já expirou (resultados disponíveis por 2 horas)"
            });
        }

        return Ok(result);
    }

    /// <summary>
    /// Obtém apenas o resumo da carteira consolidada
    /// </summary>
    [HttpGet("consolidated/{id}/summary")]
    [ProducesResponseType(typeof(PortfolioSummary), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetConsolidatedSummary(Guid id)
    {
        var result = await _consolidationService.GetConsolidatedAsync(id);
        if (result == null) return NotFound(new ErrorResponse { Error = "Não encontrado" });
        return Ok(result.Summary);
    }

    /// <summary>
    /// Obtém apenas a performance da carteira consolidada
    /// </summary>
    [HttpGet("consolidated/{id}/performance")]
    [ProducesResponseType(typeof(PortfolioPerformance), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetConsolidatedPerformance(Guid id)
    {
        var result = await _consolidationService.GetConsolidatedAsync(id);
        if (result == null) return NotFound(new ErrorResponse { Error = "Não encontrado" });
        return Ok(result.Performance);
    }

    /// <summary>
    /// Obtém apenas a análise de liquidez da carteira consolidada
    /// </summary>
    [HttpGet("consolidated/{id}/liquidity")]
    [ProducesResponseType(typeof(PortfolioLiquidity), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetConsolidatedLiquidity(Guid id)
    {
        var result = await _consolidationService.GetConsolidatedAsync(id);
        if (result == null) return NotFound(new ErrorResponse { Error = "Não encontrado" });
        return Ok(result.Liquidity);
    }

    /// <summary>
    /// Processa um arquivo individual (usado no batch)
    /// </summary>
    private async Task<BatchFileResult> ProcessSingleFileAsync(IFormFile file)
    {
        try
        {
            // Validação: Tamanho
            if (file.Length > MaxFileSizeBytes)
            {
                return new BatchFileResult
                {
                    FileName = file.FileName,
                    FileSizeBytes = file.Length,
                    Success = false,
                    Error = $"Arquivo muito grande (máximo {MaxFileSizeBytes / 1024 / 1024} MB)"
                };
            }

            // Validação: Tipo
            if (!file.ContentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase))
            {
                return new BatchFileResult
                {
                    FileName = file.FileName,
                    FileSizeBytes = file.Length,
                    Success = false,
                    Error = "Tipo de arquivo inválido (apenas PDF)"
                };
            }

            _logger.LogInformation("[BATCH] Processando: {FileName} ({FileSize} bytes)",
                file.FileName, file.Length);

            // Lê o arquivo
            byte[] fileContent;
            using (var memoryStream = new MemoryStream())
            {
                await file.CopyToAsync(memoryStream);
                fileContent = memoryStream.ToArray();
            }

            // Processa com AI dedicada
            var analysisId = await _orchestrator.ProcessPdfAsync(fileContent, file.FileName);

            _logger.LogInformation("[BATCH] ✅ Sucesso: {FileName} → {AnalysisId}",
                file.FileName, analysisId);

            return new BatchFileResult
            {
                FileName = file.FileName,
                FileSizeBytes = file.Length,
                Success = true,
                AnalysisId = analysisId,
                Endpoints = new AnalysisEndpoints
                {
                    Total = Url.Action(nameof(GetTotal), new { id = analysisId })!,
                    Classification = Url.Action(nameof(GetClassification), new { id = analysisId })!,
                    Stocks = Url.Action(nameof(GetStocks), new { id = analysisId })!,
                    FixedIncome = Url.Action(nameof(GetFixedIncome), new { id = analysisId })!
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[BATCH] ❌ Erro ao processar: {FileName}", file.FileName);

            return new BatchFileResult
            {
                FileName = file.FileName,
                FileSizeBytes = file.Length,
                Success = false,
                Error = ex.Message
            };
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
    /// Obtém os metadados completos da análise (incluindo custos, tokens, cache info)
    /// </summary>
    [HttpGet("{id}/metadata")]
    [ProducesResponseType(typeof(AnalysisResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetFullAnalysis(Guid id)
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
public class ConsolidatedAnalysisResponse
{
    public Guid ConsolidationId { get; set; }
    public int TotalFiles { get; set; }
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public string Message { get; set; } = string.Empty;
    public PortfolioSummaryDto Summary { get; set; } = new();
    public ConsolidatedEndpoints Endpoints { get; set; } = new();
}

public class PortfolioSummaryDto
{
    public decimal TotalInvested { get; set; }
    public int TotalAssets { get; set; }
    public string Currency { get; set; } = "BRL";
}

public class ConsolidatedEndpoints
{
    public string Full { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Performance { get; set; } = string.Empty;
    public string Liquidity { get; set; } = string.Empty;
}

public class BatchAnalysisResponse
{
    public int TotalFiles { get; set; }
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public List<BatchFileResult> Results { get; set; } = new();
    public string Message { get; set; } = string.Empty;
}

public class BatchFileResult
{
    public string FileName { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public bool Success { get; set; }
    public Guid? AnalysisId { get; set; }
    public string? Error { get; set; }
    public AnalysisEndpoints? Endpoints { get; set; }
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