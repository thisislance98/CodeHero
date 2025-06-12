using System;
using System.Collections.Generic;

public class ChatCommandHandler
{
    public event Action OnClearRequested;
    public event Action OnCopyRequested;
    public event Action<ChatMessage> OnMessageAdded;
    
    public void HandleCommand(string command)
    {
        string[] parts = command.Split(' ');
        string cmd = parts[0].ToLower();
        
        switch (cmd)
        {
            case "/clear":
                OnClearRequested?.Invoke();
                break;
            case "/copy":
                OnCopyRequested?.Invoke();
                break;
            case "/help":
                ShowHelp();
                break;
            case "/time":
                OnMessageAdded?.Invoke(new ChatMessage("System", $"Current time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}", MessageType.System));
                break;
            case "/warn":
                if (parts.Length > 1)
                {
                    string warningMsg = string.Join(" ", parts, 1, parts.Length - 1);
                    OnMessageAdded?.Invoke(new ChatMessage("User", warningMsg, MessageType.Warning));
                }
                break;
            case "/error":
                if (parts.Length > 1)
                {
                    string errorMsg = string.Join(" ", parts, 1, parts.Length - 1);
                    OnMessageAdded?.Invoke(new ChatMessage("User", errorMsg, MessageType.Error));
                }
                break;
            default:
                OnMessageAdded?.Invoke(new ChatMessage("System", $"Unknown command: {cmd}. Type /help for available commands.", MessageType.System));
                break;
        }
    }
    
    private void ShowHelp()
    {
        OnMessageAdded?.Invoke(new ChatMessage("System", "Available commands:", MessageType.System));
        OnMessageAdded?.Invoke(new ChatMessage("System", "/clear - Clear all messages", MessageType.System));
        OnMessageAdded?.Invoke(new ChatMessage("System", "/copy - Copy conversation to clipboard", MessageType.System));
        OnMessageAdded?.Invoke(new ChatMessage("System", "/help - Show this help message", MessageType.System));
        OnMessageAdded?.Invoke(new ChatMessage("System", "/time - Show current time", MessageType.System));
        OnMessageAdded?.Invoke(new ChatMessage("System", "/warn [message] - Send a warning message", MessageType.System));
        OnMessageAdded?.Invoke(new ChatMessage("System", "/error [message] - Send an error message", MessageType.System));
        OnMessageAdded?.Invoke(new ChatMessage("System", "", MessageType.System));
        OnMessageAdded?.Invoke(new ChatMessage("System", "UI Features:", MessageType.System));
        OnMessageAdded?.Invoke(new ChatMessage("System", "• Copy+Logs button - Toggle console log inclusion in clipboard copy", MessageType.System));
        OnMessageAdded?.Invoke(new ChatMessage("System", "• Suggestion buttons - Click to quickly send common messages", MessageType.System));
    }
} 