using UnityEngine;
using System;
using System.Collections.Generic;

[System.Serializable]
public class ChatMessage
{
    public string id;
    public string username;
    public string message;
    public string timestamp;
    public MessageType type;
    
    // Streaming state
    public bool isStreaming;
    public bool isComplete;
    
    // Token usage tracking
    public TokenUsageData tokenUsage;
    
    public ChatMessage()
    {
        id = Guid.NewGuid().ToString();
        timestamp = DateTime.Now.ToString("HH:mm:ss");
        type = MessageType.Normal;
        isStreaming = false;
        isComplete = true;
        tokenUsage = null;
    }
    
    public static ChatMessage CreateUserMessage(string username, string message)
    {
        return new ChatMessage
        {
            id = Guid.NewGuid().ToString(),
            username = username,
            message = message,
            timestamp = DateTime.Now.ToString("HH:mm:ss"),
            type = MessageType.Normal,
            isStreaming = false,
            isComplete = true
        };
    }
    
    public static ChatMessage CreateSystemMessage(string message, MessageType type = MessageType.System)
    {
        return new ChatMessage
        {
            id = Guid.NewGuid().ToString(),
            username = "System",
            message = message,
            timestamp = DateTime.Now.ToString("HH:mm:ss"),
            type = type,
            isStreaming = false,
            isComplete = true
        };
    }
    
    public static ChatMessage CreateStreamingMessage(string username, MessageType type = MessageType.Normal)
    {
        return new ChatMessage
        {
            id = Guid.NewGuid().ToString(),
            username = username,
            message = "",
            timestamp = DateTime.Now.ToString("HH:mm:ss"),
            type = type,
            isStreaming = true,
            isComplete = false
        };
    }
    
    public void SetTokenUsage(TokenUsageData usage)
    {
        tokenUsage = usage;
    }
    
    public bool HasTokenUsage()
    {
        return tokenUsage != null && (tokenUsage.inputTokens > 0 || tokenUsage.outputTokens > 0);
    }
    
    public string GetTokenUsageSummary()
    {
        if (!HasTokenUsage()) return "";
        return tokenUsage.GetUsageSummary();
    }
    
    // Method to append text during streaming
    public void AppendText(string text)
    {
        if (isStreaming)
        {
            message += text;
        }
    }
    
    // Method to complete streaming
    public void CompleteStreaming()
    {
        isStreaming = false;
        isComplete = true;
    }
}

public enum MessageType
{
    Normal,
    System,
    Warning,
    Error
}

// Unified message queue entry
[System.Serializable]
public class MessageQueueEntry
{
    public ChatMessage message;
    public bool requiresInsertionAboveStreaming; // For system/error messages during streaming
    public System.Action<string> onTextDelta; // Optional streaming callback
    public System.Action onComplete; // Optional completion callback
    
    public MessageQueueEntry(ChatMessage msg, bool insertAboveStreaming = false, 
                           System.Action<string> textCallback = null, 
                           System.Action completeCallback = null)
    {
        message = msg;
        requiresInsertionAboveStreaming = insertAboveStreaming;
        onTextDelta = textCallback;
        onComplete = completeCallback;
    }
}

[System.Serializable]
public class LogEntry
{
    public string timestamp;
    public string logString;
    public string stackTrace;
    public LogType type;
    
    public LogEntry(string log, string stack, LogType logType)
    {
        timestamp = DateTime.Now.ToString("HH:mm:ss");
        logString = log;
        stackTrace = stack;
        type = logType;
    }
}

 