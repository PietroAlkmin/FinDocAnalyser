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
    /// Upload and analyze financial PDF(s) - parallel processing
    /// </summary>
    /// <param name="files">Financial report PDF file(s) - maximum 20 files</param>
    /// <returns>Batch analysis result with individual IDs</returns>
    [HttpPost]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(BatchAnalysisResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> AnalyzePdfs(List<IFormFile> files)
    {
        try
        {
            // Validation 1: Files sent?
            if (files == null || files.Count == 0)
            {
                return BadRequest(new ErrorResponse
                {
                    Error = "No files were sent",
                    Details = "Please send at least one valid PDF file"
                });
            }

            // Validation 2: File limit
            if (files.Count > 20)
            {
                return BadRequest(new ErrorResponse
                {
                    Error = "Too many files",
                    Details = "Maximum 20 files per request"
                });
            }

            _logger.LogInformation("Starting batch analysis: {FileCount} file(s)", files.Count);

            var tasks = new List<Task<BatchFileResult>>();

            // Create a task for each PDF (parallel processing)
            foreach (var file in files)
            {
                var task = ProcessSingleFileAsync(file);
                tasks.Add(task);
            }

            // Wait for ALL PDFs to be processed in parallel
            var results = await Task.WhenAll(tasks);

            var successCount = results.Count(r => r.Success);
            var failureCount = results.Count(r => !r.Success);

            _logger.LogInformation(
                "Batch analysis completed: {SuccessCount}/{TotalCount} files processed successfully",
                successCount, files.Count);

            // Return 202 Accepted with individual results
            var response = new BatchAnalysisResponse
            {
                TotalFiles = files.Count,
                SuccessCount = successCount,
                FailureCount = failureCount,
                Results = results.ToList(),
                Message = successCount == files.Count 
                    ? $"All {successCount} files processed successfully"
                    : $"{successCount} of {files.Count} files processed successfully ({failureCount} failed)"
            };

            return Accepted(response);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Validation error processing file");
            return BadRequest(new ErrorResponse
            {
                Error = "Error processing file",
                Details = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error processing file");
            return StatusCode(500, new ErrorResponse
            {
                Error = "Internal server error",
                Details = "An error occurred processing your request. Please try again."
            });
        }
    }

    /// <summary>
    /// Get total invested amount
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
                Error = "Analysis not found",
                Details = "The analysis does not exist or has expired (results available for 30 minutes)"
            });
        }

        return Ok(result);
    }

    /// <summary>
    /// Get asset classification
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
                Error = "Analysis not found",
                Details = "The analysis does not exist or has expired (results available for 30 minutes)"
            });
        }

        return Ok(result);
    }

    /// <summary>
    /// Get stocks portfolio
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
                Error = "Analysis not found",
                Details = "The analysis does not exist or has expired (results available for 30 minutes)"
            });
        }

        return Ok(result);
    }

    /// <summary>
    /// Get fixed income portfolio
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
                Error = "Analysis not found",
                Details = "The analysis does not exist or has expired (results available for 30 minutes)"
            });
        }

        return Ok(result);
    }

    /// <summary>
    /// Get variable income portfolio
    /// </summary>
    [HttpGet("{id}/variable-income")]
    [ProducesResponseType(typeof(VariableIncomePortfolio), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetVariableIncome(Guid id)
    {
        var result = await _orchestrator.GetVariableIncomeAsync(id);

        if (result == null)
        {
            return NotFound(new ErrorResponse
            {
                Error = "Analysis not found",
                Details = "The analysis does not exist or has expired (results available for 30 minutes)"
            });
        }

        return Ok(result);
    }

    /// <summary>
    /// Get alternative assets portfolio
    /// </summary>
    [HttpGet("{id}/alternative-assets")]
    [ProducesResponseType(typeof(AlternativeAssetsPortfolio), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAlternativeAssets(Guid id)
    {
        var result = await _orchestrator.GetAlternativeAssetsAsync(id);

        if (result == null)
        {
            return NotFound(new ErrorResponse
            {
                Error = "Analysis not found",
                Details = "The analysis does not exist or has expired (results available for 30 minutes)"
            });
        }

        return Ok(result);
    }

    /// <summary>
    /// Get cash positions
    /// </summary>
    [HttpGet("{id}/cash")]
    [ProducesResponseType(typeof(CashPortfolio), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCash(Guid id)
    {
        var result = await _orchestrator.GetCashAsync(id);

        if (result == null)
        {
            return NotFound(new ErrorResponse
            {
                Error = "Analysis not found",
                Details = "The analysis does not exist or has expired (results available for 30 minutes)"
            });
        }

        return Ok(result);
    }

    /// <summary>
    /// Get complete audit information for analysis (including raw AI response)
    /// </summary>
    [HttpGet("{id}/audit")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<object>> GetAudit(Guid id)
    {
        var result = await _orchestrator.GetAnalysisAsync(id);
        
        if (result == null)
        {
            return NotFound(new ErrorResponse
            {
                Error = "Analysis not found",
                Details = "The analysis does not exist or has expired (results available for 30 minutes)"
            });
        }

        return Ok(new
        {
            analysisId = result.AnalysisId,
            createdAt = result.CreatedAt,
            expiresAt = result.ExpiresAt,
            fileName = result.FileName,
            fileSizeBytes = result.FileSizeBytes,
            fileHash = result.FileHash,
            audit = result.Audit,
            metadata = result.Metadata,
            extractedTextPreview = result.ExtractedText?.Length > 500 
                ? result.ExtractedText.Substring(0, 500) + "..." 
                : result.ExtractedText,
            extractedTextLength = result.ExtractedText?.Length ?? 0,
            rawAiResponse = result.Metadata?.RawAiResponse,
            totalAssets = new
            {
                variableIncome = result.VariableIncome?.Assets?.Count ?? 0,
                fixedIncome = result.FixedIncome?.Assets?.Count ?? 0,
                alternativeAssets = result.AlternativeAssets?.Assets?.Count ?? 0,
                cashPositions = result.Cash?.Positions?.Count ?? 0
            }
        });
    }

    /// <summary>
    /// Get extracted text from PDF
    /// </summary>
    [HttpGet("{id}/extracted-text")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetExtractedText(Guid id)
    {
        var result = await _orchestrator.GetExtractedTextAsync(id);

        if (result == null)
        {
            return NotFound(new ErrorResponse
            {
                Error = "Analysis not found",
                Details = "The analysis does not exist or has expired (results available for 30 minutes)"
            });
        }

        return Ok(new { extractedText = result });
    }

    /// <summary>
    /// Get complete analysis metadata (including costs, tokens, cache info)
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
                Error = "Analysis not found",
                Details = "The analysis does not exist or has expired (results available for 30 minutes)"
            });
        }

        return Ok(result);
    }

    /// <summary>
    /// Process a single PDF file (used internally by batch)
    /// </summary>
    private async Task<BatchFileResult> ProcessSingleFileAsync(IFormFile file)
    {
        try
        {
            // Validation: Size
            if (file.Length > MaxFileSizeBytes)
            {
                return new BatchFileResult
                {
                    FileName = file.FileName,
                    FileSizeBytes = file.Length,
                    Success = false,
                    Error = $"File too large (maximum {MaxFileSizeBytes / 1024 / 1024} MB)"
                };
            }

            // Validation: Type
            if (!file.ContentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase))
            {
                return new BatchFileResult
                {
                    FileName = file.FileName,
                    FileSizeBytes = file.Length,
                    Success = false,
                    Error = "Invalid file type (PDF only)"
                };
            }

            _logger.LogInformation("[BATCH] Processing: {FileName} ({FileSize} bytes)",
                file.FileName, file.Length);

            // Read the file
            byte[] fileContent;
            using (var memoryStream = new MemoryStream())
            {
                await file.CopyToAsync(memoryStream);
                fileContent = memoryStream.ToArray();
            }

            // Process with dedicated AI
            var analysisId = await _orchestrator.ProcessPdfAsync(fileContent, file.FileName);

            _logger.LogInformation("[BATCH] ✅ Success: {FileName} → {AnalysisId}",
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
                    FixedIncome = Url.Action(nameof(GetFixedIncome), new { id = analysisId })!,
                    VariableIncome = Url.Action(nameof(GetVariableIncome), new { id = analysisId })!,
                    AlternativeAssets = Url.Action(nameof(GetAlternativeAssets), new { id = analysisId })!,
                    Cash = Url.Action(nameof(GetCash), new { id = analysisId })!,
                    ExtractedText = Url.Action(nameof(GetExtractedText), new { id = analysisId })!
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[BATCH] ❌ Error processing: {FileName}", file.FileName);

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

// Response DTOs
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
    public string VariableIncome { get; set; } = string.Empty;
    public string AlternativeAssets { get; set; } = string.Empty;
    public string Cash { get; set; } = string.Empty;
    public string ExtractedText { get; set; } = string.Empty;
}

public class ErrorResponse
{
    public string Error { get; set; } = string.Empty;
    public string? Details { get; set; }
}