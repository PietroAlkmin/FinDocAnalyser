using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FinDocAnalyzer.Core.Interfaces;

public interface IPdfExtractor
{
    /// <summary>
    /// Extracts all text content from a PDF file
    /// </summary>
    /// <param name="pdfContent">PDF file as byte array</param>
    /// <returns>Extracted text</returns>
    Task<string> ExtractTextAsync(byte[] pdfContent);

    /// <summary>
    /// Validates if the file is a valid PDF
    /// </summary>
    /// <param name="content">File content as byte array</param>
    /// <returns>True if valid PDF, false otherwise</returns>
    bool IsValidPdf(byte[] content);
}
