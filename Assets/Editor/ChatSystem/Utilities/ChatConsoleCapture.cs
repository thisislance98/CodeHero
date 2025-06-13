using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System;
using System.Linq;

public class ChatConsoleCapture
{
    private List<LogEntry> capturedLogs = new List<LogEntry>();
    private const int MAX_LOG_ENTRIES = 500;
    
    // Error batching system
    private List<ErrorBatch> pendingErrors = new List<ErrorBatch>();
    private const float ERROR_BATCH_DELAY = 1.0f; // Reduced to 1 second for faster response
    private float lastErrorTime = 0f;
    private bool hasScheduledErrorProcessing = false;
    
    public event Action<List<ErrorBatch>> OnErrorBatchReceived;
    public bool IncludeLogs { get; set; } = true;
    
    public List<LogEntry> CapturedLogs => capturedLogs;
    
    public void StartCapturing()
    {
        Debug.Log("[ChatConsoleCapture] Starting error capture");
        Application.logMessageReceived += OnLogMessageReceived;
        EditorApplication.update += CheckForBatchedErrors;
    }
    
    public void StopCapturing()
    {
        Debug.Log("[ChatConsoleCapture] Stopping error capture");
        Application.logMessageReceived -= OnLogMessageReceived;
        EditorApplication.update -= CheckForBatchedErrors;
    }
    
    private void OnLogMessageReceived(string logString, string stackTrace, LogType type)
    {
        // Capture logs but limit the list size to prevent memory issues
        capturedLogs.Add(new LogEntry(logString, stackTrace, type));
        
        // Keep only the last MAX_LOG_ENTRIES log entries
        if (capturedLogs.Count > MAX_LOG_ENTRIES)
        {
            capturedLogs.RemoveAt(0);
        }
        
        // Route errors to error batching system
        if (type == LogType.Error || type == LogType.Exception)
        {
            Debug.Log($"[ChatConsoleCapture] Error detected: {logString}");
            AddErrorToBatch(logString, stackTrace, type);
        }
    }
    
    private void AddErrorToBatch(string logString, string stackTrace, LogType type)
    {
        Debug.Log($"[ChatConsoleCapture] Adding error to batch. Pending errors: {pendingErrors.Count}");
        
        // Check if we already have this error (deduplication)
        var existingError = pendingErrors.FirstOrDefault(e => 
            e.LogString == logString && e.StackTrace == stackTrace);
            
        if (existingError != null)
        {
            existingError.Count++;
            Debug.Log($"[ChatConsoleCapture] Duplicate error found, count increased to: {existingError.Count}");
            return;
        }
        
        // Add new error to batch
        pendingErrors.Add(new ErrorBatch
        {
            LogString = logString,
            StackTrace = stackTrace,
            LogType = type,
            Timestamp = DateTime.Now,
            Count = 1
        });
        
        lastErrorTime = (float)EditorApplication.timeSinceStartup;
        
        if (!hasScheduledErrorProcessing)
        {
            hasScheduledErrorProcessing = true;
            Debug.Log($"[ChatConsoleCapture] Scheduled error processing. Will process in {ERROR_BATCH_DELAY} seconds");
        }
        
        Debug.Log($"[ChatConsoleCapture] Total pending errors: {pendingErrors.Count}");
    }
    
    private void CheckForBatchedErrors()
    {
        if (!hasScheduledErrorProcessing || pendingErrors.Count == 0)
            return;
            
        float timeSinceLastError = (float)EditorApplication.timeSinceStartup - lastErrorTime;
        
        if (timeSinceLastError >= ERROR_BATCH_DELAY)
        {
            Debug.Log($"[ChatConsoleCapture] Processing batched errors after {timeSinceLastError:F2} seconds");
            ProcessBatchedErrors();
        }
    }
    
    private void ProcessBatchedErrors()
    {
        if (pendingErrors.Count == 0)
        {
            Debug.Log("[ChatConsoleCapture] No errors to process");
            return;
        }
            
        Debug.Log($"[ChatConsoleCapture] Processing {pendingErrors.Count} batched errors");
        
        var errorBatch = new List<ErrorBatch>(pendingErrors);
        pendingErrors.Clear();
        hasScheduledErrorProcessing = false;
        
        OnErrorBatchReceived?.Invoke(errorBatch);
        
        Debug.Log("[ChatConsoleCapture] Error batch sent to ChatWindow");
    }
    
    public void ClearLogs()
    {
        capturedLogs.Clear();
        pendingErrors.Clear();
        hasScheduledErrorProcessing = false;
    }
    
    public bool HasRecentErrors()
    {
        // Check if we have any errors from the last few seconds
        const float recentTimeWindow = 5.0f; // 5 seconds
        
        // Check if we have any pending errors (these are recent by definition)
        if (pendingErrors.Count > 0)
        {
            return true;
        }
        
        // Check if we have any recent errors based on when they were added
        var cutoffTime = DateTime.Now.AddSeconds(-recentTimeWindow);
        
        // Check pending errors by their timestamp
        foreach (var error in pendingErrors)
        {
            if (error.Timestamp > cutoffTime)
            {
                return true;
            }
        }
        
        // For captured logs, we'll look at the most recent entries since we don't have precise DateTime
        // We'll check the last few log entries for errors (since they're added chronologically)
        const int recentLogCount = 10; // Check last 10 log entries
        int startIndex = Math.Max(0, capturedLogs.Count - recentLogCount);
        
        for (int i = startIndex; i < capturedLogs.Count; i++)
        {
            var log = capturedLogs[i];
            if (log.type == LogType.Error || log.type == LogType.Exception)
            {
                return true;
            }
        }
        
        return false;
    }
}

[System.Serializable]
public class ErrorBatch
{
    public string LogString;
    public string StackTrace;
    public LogType LogType;
    public DateTime Timestamp;
    public int Count;
} 