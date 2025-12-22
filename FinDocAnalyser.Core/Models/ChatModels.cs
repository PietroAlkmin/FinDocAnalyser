using System;
using System.Collections.Generic;

namespace FinDocAnalyzer.Core.Models;

/// <summary>
/// Request model for asking questions about an analysis
/// </summary>
public class ChatRequest
{
    /// <summary>
    /// User's question about the analysis
    /// </summary>
    public string Question { get; set; } = string.Empty;
    
    /// <summary>
    /// Optional conversation history for context
    /// </summary>
    public List<ChatMessage>? ConversationHistory { get; set; }
}

/// <summary>
/// Response model for chat interactions
/// </summary>
public class ChatResponse
{
    /// <summary>
    /// AI's answer to the user's question
    /// </summary>
    public string Answer { get; set; } = string.Empty;
    
    /// <summary>
    /// Sources/evidence used to generate the answer
    /// </summary>
    public List<string> Sources { get; set; } = new();
    
    /// <summary>
    /// Confidence level (0.0 to 1.0)
    /// </summary>
    public decimal Confidence { get; set; }
    
    /// <summary>
    /// Updated conversation history (includes this exchange)
    /// </summary>
    public List<ChatMessage> ConversationHistory { get; set; } = new();
    
    /// <summary>
    /// Timestamp of the response
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Individual message in a conversation
/// </summary>
public class ChatMessage
{
    /// <summary>
    /// Role: "user" or "assistant"
    /// </summary>
    public string Role { get; set; } = string.Empty;
    
    /// <summary>
    /// Message content
    /// </summary>
    public string Content { get; set; } = string.Empty;
    
    /// <summary>
    /// When the message was sent
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
