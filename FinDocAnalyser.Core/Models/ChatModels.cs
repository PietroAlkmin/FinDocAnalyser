using System;
using System.Collections.Generic;

namespace FinDocAnalyzer.Core.Models;

/// <summary>
/// Simple chat request - just a message
/// </summary>
public class ChatRequest
{
    /// <summary>
    /// User's question about the analysis
    /// </summary>
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Simple chat response - just an answer
/// </summary>
public class ChatResponse
{
    /// <summary>
    /// AI's answer to the user's question
    /// </summary>
    public string Answer { get; set; } = string.Empty;
}
