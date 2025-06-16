using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Text;
using System;
using Newtonsoft.Json;
using System.Linq;

public static class UnityLogTools
{
    public static List<ClaudeTool> GetUnityLogTools()
    {
        return new List<ClaudeTool>
        {
            new ClaudeTool
            {
                name = "read_unity_logs",
                description = "Read Unity console logs to debug compilation issues, runtime errors, or verify successful operations. Can filter by log type and limit the number of entries returned.",
                input_schema = new ClaudeToolInputSchema
                {
                    properties = new Dictionary<string, ClaudeToolProperty>
                    {
                        ["log_type"] = new ClaudeToolProperty 
                        { 
                            type = "string", 
                            description = "Filter by log type: 'All', 'Error', 'Warning', 'Log', 'Exception', 'Assert' (default: 'All')" 
                        },
                        ["max_entries"] = new ClaudeToolProperty 
                        { 
                            type = "string", 
                            description = "Maximum number of log entries to return (default: 50, max: 200)" 
                        },
                        ["recent_only"] = new ClaudeToolProperty 
                        { 
                            type = "string", 
                            description = "Only return recent logs from the last few minutes (true/false, default: false)" 
                        },
                        ["include_stack_trace"] = new ClaudeToolProperty 
                        { 
                            type = "string", 
                            description = "Include stack traces for errors and exceptions (true/false, default: true)" 
                        }
                    },
                    required = new List<string>()
                }
            },
            new ClaudeTool
            {
                name = "check_compilation_status",
                description = "Check the current Unity compilation status and any recent compilation errors.",
                input_schema = new ClaudeToolInputSchema
                {
                    properties = new Dictionary<string, ClaudeToolProperty>(),
                    required = new List<string>()
                }
            },
            new ClaudeTool
            {
                name = "clear_console",
                description = "Clear the Unity console to start fresh for debugging.",
                input_schema = new ClaudeToolInputSchema
                {
                    properties = new Dictionary<string, ClaudeToolProperty>(),
                    required = new List<string>()
                }
            }
        };
    }
    
    public static string ExecuteUnityLogTool(ClaudeToolUse toolUse)
    {
        var inputDict = JsonConvert.DeserializeObject<Dictionary<string, object>>(
            JsonConvert.SerializeObject(toolUse.input));
        
        switch (toolUse.name)
        {
            case "read_unity_logs":
                Debug.Log("[ClaudeAI] UnityLogTools: Calling ReadUnityLogs");
                return ReadUnityLogs(inputDict);
                
            case "check_compilation_status":
                Debug.Log("[ClaudeAI] UnityLogTools: Calling CheckCompilationStatus");
                return CheckCompilationStatus();
                
            case "clear_console":
                Debug.Log("[ClaudeAI] UnityLogTools: Calling ClearConsole");
                return ClearConsole();
                
            default:
                return $"Unknown Unity log tool: {toolUse.name}";
        }
    }
    
    private static string ReadUnityLogs(Dictionary<string, object> input)
    {
        try
        {
            // Parse parameters with defaults
            var logTypeFilter = input.ContainsKey("log_type") ? input["log_type"].ToString() : "All";
            var maxEntriesStr = input.ContainsKey("max_entries") ? input["max_entries"].ToString() : "50";
            var recentOnlyStr = input.ContainsKey("recent_only") ? input["recent_only"].ToString() : "false";
            var includeStackTraceStr = input.ContainsKey("include_stack_trace") ? input["include_stack_trace"].ToString() : "true";
            
            if (!int.TryParse(maxEntriesStr, out int maxEntries))
                maxEntries = 50;
            
            // Cap max entries to prevent overwhelming output
            maxEntries = Math.Min(maxEntries, 200);
            
            bool recentOnly = recentOnlyStr.ToLower() == "true";
            bool includeStackTrace = includeStackTraceStr.ToLower() == "true";
            
            // Access the console capture system used by the chat window
            var consoleCapture = FindConsoleCapture();
            if (consoleCapture == null)
            {
                return "Unable to access Unity console logs. The console capture system may not be initialized.";
            }
            
            var logs = consoleCapture.CapturedLogs;
            if (logs == null || logs.Count == 0)
            {
                return "No Unity console logs found. The console may be empty or the capture system may not be running.";
            }
            
            // Filter logs based on parameters
            var filteredLogs = logs.AsEnumerable();
            
            // Filter by log type
            if (logTypeFilter != "All")
            {
                LogType targetType;
                if (Enum.TryParse<LogType>(logTypeFilter, out targetType))
                {
                    filteredLogs = filteredLogs.Where(log => log.type == targetType);
                }
            }
            
            // Filter by recency if requested (last 5 minutes)
            if (recentOnly)
            {
                var recentCutoff = DateTime.Now.AddMinutes(-5);
                // Since LogEntry doesn't have DateTime, we'll just take the most recent entries
                var recentCount = Math.Min(logs.Count, 20);
                filteredLogs = logs.Skip(logs.Count - recentCount);
            }
            
            // Limit the number of entries
            var finalLogs = filteredLogs.Reverse().Take(maxEntries).Reverse().ToList();
            
            if (finalLogs.Count == 0)
            {
                return $"No Unity console logs found matching the criteria (log_type: {logTypeFilter}, recent_only: {recentOnly}).";
            }
            
            // Format the output
            var result = new StringBuilder();
            result.AppendLine($"Unity Console Logs ({finalLogs.Count} entries, filtered by: {logTypeFilter}):");
            result.AppendLine(new string('=', 60));
            
            int entryNumber = 1;
            foreach (var log in finalLogs)
            {
                result.AppendLine($"\n[{entryNumber}] {GetLogTypeIcon(log.type)} {log.type}: {log.logString}");
                result.AppendLine($"    Time: {log.timestamp}");
                
                if (includeStackTrace && !string.IsNullOrEmpty(log.stackTrace) && 
                    (log.type == LogType.Error || log.type == LogType.Exception))
                {
                    result.AppendLine("    Stack Trace:");
                    var stackLines = log.stackTrace.Split('\n');
                    foreach (var line in stackLines.Take(5)) // Limit stack trace lines
                    {
                        if (!string.IsNullOrWhiteSpace(line))
                            result.AppendLine($"      {line.Trim()}");
                    }
                    if (stackLines.Length > 5)
                        result.AppendLine("      ... (truncated)");
                }
                
                entryNumber++;
            }
            
            result.AppendLine($"\n{new string('=', 60)}");
            result.AppendLine($"Total entries shown: {finalLogs.Count}");
            
            return result.ToString();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ClaudeAI] Error reading Unity logs: {ex.Message}");
            return $"Error reading Unity logs: {ex.Message}";
        }
    }
    
    private static string CheckCompilationStatus()
    {
        try
        {
            var result = new StringBuilder();
            result.AppendLine("Unity Compilation Status:");
            result.AppendLine(new string('=', 30));
            
            bool isCompiling = EditorApplication.isCompiling;
            result.AppendLine($"Currently Compiling: {(isCompiling ? "Yes" : "No")}");
            
            if (isCompiling)
            {
                result.AppendLine("🔄 Unity is currently compiling scripts...");
            }
            else
            {
                result.AppendLine("✅ Compilation is not running");
            }
            
            // Check for recent compilation errors
            var consoleCapture = FindConsoleCapture();
            if (consoleCapture != null)
            {
                bool hasRecentErrors = consoleCapture.HasRecentErrors();
                result.AppendLine($"Recent Errors: {(hasRecentErrors ? "Yes" : "No")}");
                
                if (hasRecentErrors)
                {
                    result.AppendLine("⚠️ There are recent compilation or runtime errors");
                    result.AppendLine("💡 Use 'read_unity_logs' with log_type='Error' to see details");
                }
                else
                {
                    result.AppendLine("✅ No recent errors detected");
                }
            }
            
            return result.ToString();
        }
        catch (Exception ex)
        {
            return $"Error checking compilation status: {ex.Message}";
        }
    }
    
    private static string ClearConsole()
    {
        try
        {
            // Clear the Unity console
            var assembly = System.Reflection.Assembly.GetAssembly(typeof(EditorWindow));
            var type = assembly.GetType("UnityEditor.LogEntries");
            var method = type.GetMethod("Clear");
            method.Invoke(new object(), null);
            
            // Also clear our captured logs
            var consoleCapture = FindConsoleCapture();
            if (consoleCapture != null)
            {
                consoleCapture.ClearLogs();
            }
            
            return "✅ Unity console cleared successfully";
        }
        catch (Exception ex)
        {
            return $"Error clearing console: {ex.Message}";
        }
    }
    
    private static ChatConsoleCapture FindConsoleCapture()
    {
        // Try to find the console capture instance from the chat window
        var chatWindows = Resources.FindObjectsOfTypeAll<ChatWindow>();
        if (chatWindows.Length > 0)
        {
            var chatWindow = chatWindows[0];
            // Access the console capture through reflection since it's private
            var field = typeof(ChatWindow).GetField("consoleCapture", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null)
            {
                return field.GetValue(chatWindow) as ChatConsoleCapture;
            }
        }
        
        return null;
    }
    
    private static string GetLogTypeIcon(LogType logType)
    {
        switch (logType)
        {
            case LogType.Error:
                return "❌";
            case LogType.Warning:
                return "⚠️";
            case LogType.Log:
                return "ℹ️";
            case LogType.Exception:
                return "💥";
            case LogType.Assert:
                return "⚡";
            default:
                return "📝";
        }
    }
} 