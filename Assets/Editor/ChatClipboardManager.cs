using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Text;
using System;

public static class ChatClipboardManager
{
    public static void CopyConversationToClipboard(List<ChatMessage> messages, List<LogEntry> capturedLogs, bool includeLogs)
    {
        if (messages.Count == 0)
        {
            EditorGUIUtility.systemCopyBuffer = "No messages to copy.";
            return;
        }
        
        var conversationText = new StringBuilder();
        conversationText.AppendLine("=== Unity Chat Window Conversation ===");
        conversationText.AppendLine($"Exported on: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        conversationText.AppendLine();
        
        foreach (var message in messages)
        {
            string messageTypePrefix = GetMessageTypePrefix(message.type);
            
            if (message.type == MessageType.System)
            {
                conversationText.AppendLine($"{message.timestamp} - {messageTypePrefix}[{message.username}]: {message.message}");
            }
            else
            {
                conversationText.AppendLine($"{message.timestamp} - {messageTypePrefix}{message.username}: {message.message}");
            }
        }
        
        conversationText.AppendLine();
        conversationText.AppendLine("=== End of Conversation ===");
        
        // Include console logs if enabled
        if (includeLogs && capturedLogs.Count > 0)
        {
            AppendConsoleLogs(conversationText, capturedLogs);
        }
        
        EditorGUIUtility.systemCopyBuffer = conversationText.ToString();
    }
    
    private static string GetMessageTypePrefix(MessageType type)
    {
        switch (type)
        {
            case MessageType.System:
                return "[SYSTEM] ";
            case MessageType.Warning:
                return "[WARNING] ";
            case MessageType.Error:
                return "[ERROR] ";
            default:
                return "";
        }
    }
    
    private static void AppendConsoleLogs(StringBuilder conversationText, List<LogEntry> capturedLogs)
    {
        conversationText.AppendLine();
        conversationText.AppendLine("=== Console Logs ===");
        conversationText.AppendLine($"Captured {capturedLogs.Count} log entries during this session:");
        conversationText.AppendLine();
        
        foreach (var logEntry in capturedLogs)
        {
            string logTypePrefix = GetLogTypePrefix(logEntry.type);
            conversationText.AppendLine($"{logEntry.timestamp} - {logTypePrefix}{logEntry.logString}");
            
            // Include stack trace for errors and exceptions
            if ((logEntry.type == LogType.Error || logEntry.type == LogType.Exception) && 
                !string.IsNullOrEmpty(logEntry.stackTrace))
            {
                conversationText.AppendLine($"Stack Trace: {logEntry.stackTrace}");
                conversationText.AppendLine();
            }
        }
        
        conversationText.AppendLine("=== End of Console Logs ===");
    }
    
    private static string GetLogTypePrefix(LogType type)
    {
        switch (type)
        {
            case LogType.Error:
                return "[ERROR] ";
            case LogType.Warning:
                return "[WARNING] ";
            case LogType.Log:
                return "[LOG] ";
            case LogType.Exception:
                return "[EXCEPTION] ";
            case LogType.Assert:
                return "[ASSERT] ";
            default:
                return "";
        }
    }
} 