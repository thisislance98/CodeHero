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
    
    public ChatMessage(string user, string msg, MessageType msgType = MessageType.Normal, bool streamingMode = false)
    {
        id = System.Guid.NewGuid().ToString();
        username = user;
        message = msg;
        timestamp = DateTime.Now.ToString("HH:mm:ss");
        type = msgType;
        isStreaming = streamingMode;
        isComplete = !streamingMode;
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

 