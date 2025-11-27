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
    /// Upload e análise de PDF(s) financeiro(s) - processamento em paralelo
    /// </summary>
    /// <param name="files">Arquivo(s) PDF do(s) relatório(s) financeiro(s) - máximo 20 arquivos</param>
    /// <returns>Resultado da análise em batch com IDs individuais</returns>
    [HttpPost]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(BatchAnalysisResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> AnalyzePdfs(List<IFormFile> files)
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

            _logger.LogInformation("Iniciando análise em batch: {FileCount} arquivo(s)", files.Count);

            var tasks = new List<Task<BatchFileResult>>();

            // Cria uma task para cada PDF (processamento paralelo)
            foreach (var file in files)
            {
                var task = ProcessSingleFileAsync(file);
                tasks.Add(task);
            }

            // Aguarda TODOS os PDFs serem processados em paralelo
            var results = await Task.WhenAll(tasks);

            var successCount = results.Count(r => r.Success);
            var failureCount = results.Count(r => !r.Success);

            _logger.LogInformation(
                "Análise em batch concluída: {SuccessCount}/{TotalCount} arquivos processados com sucesso",
                successCount, files.Count);

            // Retorna 202 Accepted com resultados individuais
            var response = new BatchAnalysisResponse
            {
                TotalFiles = files.Count,
                SuccessCount = successCount,
                FailureCount = failureCount,
                Results = results.ToList(),
                Message = successCount == files.Count 
                    ? $"Todos os {successCount} arquivos processados com sucesso"
                    : $"{successCount} de {files.Count} arquivos processados com sucesso ({failureCount} falharam)"
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

    /// <summary>
    /// Processa um único arquivo PDF (usado internamente pelo batch)
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
}

// DTOs para respostas
public class AnalysisResponse
{
    public Guid AnalysisId { get; set; }
    public string Message { get; set; } = string.Empty;
    public AnalysisEndpoints Endpoints { get; set; } = new();
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