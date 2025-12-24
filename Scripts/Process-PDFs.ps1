#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Batch process multiple PDFs through FinDocAnalyser API and export results to Excel.

.DESCRIPTION
    This script automates the processing of financial PDFs:
    1. Uploads PDFs to the analysis API
    2. Waits for processing to complete
    3. Exports each analysis as an Excel file with multiple sheets
    
    Designed for the 96 PDF POC requirement - processes all PDFs in a folder
    and generates formatted Excel spreadsheets for client delivery.

.PARAMETER PdfFolder
    Path to folder containing PDF files to process

.PARAMETER OutputFolder
    Path where Excel files will be saved (created if doesn't exist)

.PARAMETER ApiUrl
    Base URL of the FinDocAnalyser API (default: http://localhost:5070/api/analysis)

.PARAMETER MaxConcurrent
    Maximum number of PDFs to process concurrently (default: 5)

.PARAMETER WaitForCompletion
    Wait for all analyses to complete before exporting (default: $true)

.PARAMETER TimeoutSeconds
    Timeout in seconds for each analysis (default: 300 = 5 minutes)

.EXAMPLE
    .\Process-PDFs.ps1 -PdfFolder "C:\PDFs\Cliente" -OutputFolder "C:\Resultados"
    
    Process all PDFs in C:\PDFs\Cliente and save Excel files to C:\Resultados

.EXAMPLE
    .\Process-PDFs.ps1 -PdfFolder ".\96_PDFs" -OutputFolder ".\Output" -MaxConcurrent 10
    
    Process PDFs with higher concurrency (10 at a time)

.NOTES
    Author: FinDocAnalyser
    Date: 2025
    Requires: PowerShell 7+ for proper REST API handling
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory=$true, HelpMessage="Path to folder containing PDF files")]
    [ValidateScript({Test-Path $_ -PathType Container})]
    [string]$PdfFolder,

    [Parameter(Mandatory=$true, HelpMessage="Path where Excel files will be saved")]
    [string]$OutputFolder,

    [Parameter(HelpMessage="Base URL of the FinDocAnalyser API")]
    [string]$ApiUrl = "http://localhost:5070/api/analysis",

    [Parameter(HelpMessage="Maximum number of PDFs to process concurrently")]
    [ValidateRange(1, 20)]
    [int]$MaxConcurrent = 5,

    [Parameter(HelpMessage="Wait for analyses to complete before exporting")]
    [bool]$WaitForCompletion = $true,

    [Parameter(HelpMessage="Timeout in seconds for each analysis")]
    [int]$TimeoutSeconds = 300
)

# Enable strict mode
Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# Create output folder if it doesn't exist
if (-not (Test-Path $OutputFolder)) {
    Write-Host "Creating output folder: $OutputFolder" -ForegroundColor Cyan
    New-Item -ItemType Directory -Path $OutputFolder -Force | Out-Null
}

# Get all PDF files
$pdfFiles = Get-ChildItem -Path $PdfFolder -Filter "*.pdf" -File
$totalFiles = $pdfFiles.Count

if ($totalFiles -eq 0) {
    Write-Warning "No PDF files found in $PdfFolder"
    exit 0
}

Write-Host "`n=== FinDocAnalyser Batch Processor ===" -ForegroundColor Green
Write-Host "PDFs to process: $totalFiles" -ForegroundColor Cyan
Write-Host "API URL: $ApiUrl" -ForegroundColor Cyan
Write-Host "Output folder: $OutputFolder" -ForegroundColor Cyan
Write-Host "Max concurrent: $MaxConcurrent" -ForegroundColor Cyan
Write-Host ""

# Progress tracking
$results = @()
$successful = 0
$failed = 0
$startTime = Get-Date

# Function to upload and analyze a PDF
function Submit-PdfAnalysis {
    param(
        [string]$FilePath,
        [string]$ApiUrl
    )
    
    try {
        Write-Host "[$(Get-Date -Format 'HH:mm:ss')] Uploading: $(Split-Path $FilePath -Leaf)" -ForegroundColor Yellow
        
        # Create multipart form data
        $form = @{
            files = Get-Item $FilePath
        }
        
        # Upload PDF
        $response = Invoke-RestMethod -Uri $ApiUrl -Method Post -Form $form -ContentType "multipart/form-data"
        
        if ($response.results -and $response.results.Count -gt 0) {
            $analysisId = $response.results[0].analysisId
            Write-Host "[$(Get-Date -Format 'HH:mm:ss')] ✓ Submitted: $(Split-Path $FilePath -Leaf) -> $analysisId" -ForegroundColor Green
            
            return @{
                FileName = Split-Path $FilePath -Leaf
                AnalysisId = $analysisId
                Status = "Submitted"
                SubmittedAt = Get-Date
            }
        }
        else {
            throw "Invalid API response: No analysis ID returned"
        }
    }
    catch {
        Write-Host "[$(Get-Date -Format 'HH:mm:ss')] ✗ Failed: $(Split-Path $FilePath -Leaf) - $($_.Exception.Message)" -ForegroundColor Red
        return @{
            FileName = Split-Path $FilePath -Leaf
            AnalysisId = $null
            Status = "Failed"
            Error = $_.Exception.Message
        }
    }
}

# Function to export analysis to Excel
function Export-Analysis {
    param(
        [string]$AnalysisId,
        [string]$FileName,
        [string]$ApiUrl,
        [string]$OutputFolder
    )
    
    try {
        $exportUrl = "$ApiUrl/$AnalysisId/export"
        $outputPath = Join-Path $OutputFolder "$([System.IO.Path]::GetFileNameWithoutExtension($FileName)).xlsx"
        
        Write-Host "[$(Get-Date -Format 'HH:mm:ss')] Exporting: $FileName" -ForegroundColor Yellow
        
        # Download Excel file
        Invoke-RestMethod -Uri $exportUrl -Method Get -OutFile $outputPath
        
        Write-Host "[$(Get-Date -Format 'HH:mm:ss')] ✓ Exported: $FileName -> $(Split-Path $outputPath -Leaf)" -ForegroundColor Green
        
        return @{
            Status = "Success"
            OutputPath = $outputPath
        }
    }
    catch {
        Write-Host "[$(Get-Date -Format 'HH:mm:ss')] ✗ Export failed: $FileName - $($_.Exception.Message)" -ForegroundColor Red
        return @{
            Status = "Failed"
            Error = $_.Exception.Message
        }
    }
}

# STEP 1: Upload all PDFs (with concurrency control)
Write-Host "`n--- STEP 1: Uploading PDFs ---" -ForegroundColor Magenta

$jobs = @()
$processedCount = 0

foreach ($pdf in $pdfFiles) {
    # Wait if we have too many concurrent jobs
    while (($jobs | Where-Object { -not $_.Completed }).Count -ge $MaxConcurrent) {
        Start-Sleep -Milliseconds 500
    }
    
    # Submit PDF asynchronously
    $job = Start-ThreadJob -ScriptBlock {
        param($FilePath, $ApiUrl, $FunctionDef)
        
        # Re-define function in thread
        . ([ScriptBlock]::Create($FunctionDef))
        
        Submit-PdfAnalysis -FilePath $FilePath -ApiUrl $ApiUrl
    } -ArgumentList $pdf.FullName, $ApiUrl, ${function:Submit-PdfAnalysis}.ToString()
    
    $jobs += $job
    $processedCount++
    
    Write-Progress -Activity "Uploading PDFs" -Status "$processedCount / $totalFiles" -PercentComplete (($processedCount / $totalFiles) * 100)
}

# Wait for all upload jobs to complete
Write-Host "Waiting for uploads to complete..." -ForegroundColor Cyan
$results = $jobs | Wait-Job | Receive-Job
$jobs | Remove-Job

Write-Progress -Activity "Uploading PDFs" -Completed

# Count successes and failures
$successful = ($results | Where-Object { $_.Status -eq "Submitted" }).Count
$failed = ($results | Where-Object { $_.Status -eq "Failed" }).Count

Write-Host "`n--- Upload Summary ---" -ForegroundColor Magenta
Write-Host "✓ Successful: $successful" -ForegroundColor Green
Write-Host "✗ Failed: $failed" -ForegroundColor Red

# STEP 2: Wait for analyses to complete (optional)
if ($WaitForCompletion) {
    Write-Host "`n--- STEP 2: Waiting for analyses (timeout: ${TimeoutSeconds}s each) ---" -ForegroundColor Magenta
    
    $pending = $results | Where-Object { $_.Status -eq "Submitted" }
    
    foreach ($result in $pending) {
        $elapsed = 0
        $waitInterval = 5 # seconds
        
        Write-Host "[$(Get-Date -Format 'HH:mm:ss')] Waiting for: $($result.FileName)" -ForegroundColor Yellow
        
        # Poll until complete or timeout
        while ($elapsed -lt $TimeoutSeconds) {
            try {
                $checkUrl = "$ApiUrl/$($result.AnalysisId)/metadata"
                $analysisData = Invoke-RestMethod -Uri $checkUrl -Method Get -ErrorAction SilentlyContinue
                
                if ($analysisData -and $analysisData.total) {
                    Write-Host "[$(Get-Date -Format 'HH:mm:ss')] ✓ Ready: $($result.FileName)" -ForegroundColor Green
                    $result.Status = "Ready"
                    break
                }
            }
            catch {
                # Analysis not ready yet - continue waiting
            }
            
            Start-Sleep -Seconds $waitInterval
            $elapsed += $waitInterval
        }
        
        if ($result.Status -ne "Ready") {
            Write-Host "[$(Get-Date -Format 'HH:mm:ss')] ✗ Timeout: $($result.FileName)" -ForegroundColor Red
            $result.Status = "Timeout"
        }
    }
}
else {
    Write-Host "`n--- STEP 2: Skipped (WaitForCompletion=false) ---" -ForegroundColor Magenta
    Write-Host "Proceeding to export immediately (analyses may still be processing)" -ForegroundColor Yellow
}

# STEP 3: Export all successful analyses to Excel
Write-Host "`n--- STEP 3: Exporting to Excel ---" -ForegroundColor Magenta

$exportJobs = @()
$readyResults = $results | Where-Object { $_.Status -in @("Submitted", "Ready") }
$exportCount = $readyResults.Count

if ($exportCount -eq 0) {
    Write-Warning "No analyses available for export"
    exit 1
}

$exportedCount = 0

foreach ($result in $readyResults) {
    # Wait if we have too many concurrent jobs
    while (($exportJobs | Where-Object { -not $_.Completed }).Count -ge $MaxConcurrent) {
        Start-Sleep -Milliseconds 500
    }
    
    # Export asynchronously
    $job = Start-ThreadJob -ScriptBlock {
        param($AnalysisId, $FileName, $ApiUrl, $OutputFolder, $FunctionDef)
        
        # Re-define function in thread
        . ([ScriptBlock]::Create($FunctionDef))
        
        Export-Analysis -AnalysisId $AnalysisId -FileName $FileName -ApiUrl $ApiUrl -OutputFolder $OutputFolder
    } -ArgumentList $result.AnalysisId, $result.FileName, $ApiUrl, $OutputFolder, ${function:Export-Analysis}.ToString()
    
    $exportJobs += $job
    $exportedCount++
    
    Write-Progress -Activity "Exporting to Excel" -Status "$exportedCount / $exportCount" -PercentComplete (($exportedCount / $exportCount) * 100)
}

# Wait for all export jobs to complete
Write-Host "Waiting for exports to complete..." -ForegroundColor Cyan
$exportResults = $exportJobs | Wait-Job | Receive-Job
$exportJobs | Remove-Job

Write-Progress -Activity "Exporting to Excel" -Completed

# Count export successes
$exportedSuccess = ($exportResults | Where-Object { $_.Status -eq "Success" }).Count
$exportedFailed = ($exportResults | Where-Object { $_.Status -eq "Failed" }).Count

# FINAL SUMMARY
$endTime = Get-Date
$duration = $endTime - $startTime

Write-Host "`n======================================" -ForegroundColor Green
Write-Host "=== BATCH PROCESSING COMPLETE ===" -ForegroundColor Green
Write-Host "======================================" -ForegroundColor Green
Write-Host ""
Write-Host "Total PDFs: $totalFiles" -ForegroundColor Cyan
Write-Host "Upload Success: $successful" -ForegroundColor Green
Write-Host "Upload Failed: $failed" -ForegroundColor Red
Write-Host "Exported Success: $exportedSuccess" -ForegroundColor Green
Write-Host "Exported Failed: $exportedFailed" -ForegroundColor Red
Write-Host ""
Write-Host "Duration: $($duration.ToString('hh\:mm\:ss'))" -ForegroundColor Cyan
Write-Host "Output Folder: $OutputFolder" -ForegroundColor Cyan
Write-Host ""

# Exit with appropriate code
if ($exportedFailed -eq 0 -and $failed -eq 0) {
    Write-Host "✓ All operations completed successfully!" -ForegroundColor Green
    exit 0
}
else {
    Write-Warning "Some operations failed. Check output above for details."
    exit 1
}
