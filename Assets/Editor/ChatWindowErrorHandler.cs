using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Threading.Tasks;

public class ChatWindowErrorHandler
{
    // Error queue system
    private Queue<List<ErrorBatch>> pendingErrorBatches = new Queue<List<ErrorBatch>>();
    private bool hasQueuedErrors = false;
    
    // Message tracking for robust removal
    private ChatMessage currentAnalyzingMessage = null;
    private List<ErrorBatch> currentErrorBatch = null;
    private int errorFixAttempts = 0;
    private const int MAX_ERROR_FIX_ATTEMPTS = 3;
    private List<string> fixSummary = new List<string>();
    private bool isInErrorFixingCycle = false;
    
    // References to parent window components
    private ChatWindow parentWindow;
    private Action<ChatMessage> addMessageCallback;
    private Action<ChatMessage, string> removeMessageCallback;
    private Func<bool> isWaitingForAICallback;
    private Action<bool> setWaitingForAICallback;
    private Action scrollToBottomCallback;
    private Action repaintCallback;
    
    // Events
    public event Action<bool> OnErrorFixingCompleted;
    
    public ChatWindowErrorHandler(ChatWindow parent)
    {
        parentWindow = parent;
    }
    
    public void Initialize(
        Action<ChatMessage> addMessage,
        Action<ChatMessage, string> removeMessage,
        Func<bool> isWaitingForAI,
        Action<bool> setWaitingForAI,
        Action scrollToBottom,
        Action repaint)
    {
        addMessageCallback = addMessage;
        removeMessageCallback = removeMessage;
        isWaitingForAICallback = isWaitingForAI;
        setWaitingForAICallback = setWaitingForAI;
        scrollToBottomCallback = scrollToBottom;
        repaintCallback = repaint;
    }
    
    public bool IsInErrorFixingCycle => isInErrorFixingCycle;
    

    
    public void OnErrorBatchReceived(List<ErrorBatch> errorBatch, bool aiEnabled, ChatConsoleCapture consoleCapture, ChatSuggestionSystem suggestionSystem, List<ChatMessage> messages)
    {
        EditorApplication.delayCall += () =>
        {
            Debug.Log($"[ChatWindowErrorHandler] OnErrorBatchReceived called with {errorBatch.Count} errors, isWaitingForAI: {isWaitingForAICallback()}, isInErrorFixingCycle: {isInErrorFixingCycle}");
            
            // If we're already in an error fixing cycle, ignore new errors for now
            if (isInErrorFixingCycle)
            {
                Debug.Log("[ChatWindowErrorHandler] Already in error fixing cycle, ignoring new error batch");
                return;
            }
            
            // Create a single consolidated error message
            var errorSummary = CreateErrorSummary(errorBatch);
            addMessageCallback(new ChatMessage("System", errorSummary, MessageType.Error));
            
            if (aiEnabled)
            {
                if (!isWaitingForAICallback())
                {
                    Debug.Log("[ChatWindowErrorHandler] Starting error fix attempt immediately - beginning error fixing cycle");
                    AttemptBatchErrorFix(errorBatch, suggestionSystem, messages);
                }
                else
                {
                    Debug.Log("[ChatWindowErrorHandler] AI is busy or compiling, queueing error batch for later");
                    pendingErrorBatches.Enqueue(errorBatch);
                    hasQueuedErrors = true;
                }
            }
            else
            {
                Debug.Log("[ChatWindowErrorHandler] AI is disabled, skipping error fix");
            }
        };
    }
    
    public void ProcessQueuedErrors(ChatSuggestionSystem suggestionSystem, List<ChatMessage> messages)
    {
        if (hasQueuedErrors && pendingErrorBatches.Count > 0 && !isWaitingForAICallback())
        {
            Debug.Log($"[ChatWindowErrorHandler] Processing {pendingErrorBatches.Count} queued error batches");
            
            var nextErrorBatch = pendingErrorBatches.Dequeue();
            
            if (pendingErrorBatches.Count == 0)
            {
                hasQueuedErrors = false;
            }
            
            // Process the next error batch
            EditorApplication.delayCall += () => AttemptBatchErrorFix(nextErrorBatch, suggestionSystem, messages);
        }
        else if (hasQueuedErrors && pendingErrorBatches.Count == 0)
        {
            hasQueuedErrors = false;
            Debug.Log("[ChatWindowErrorHandler] No more queued errors to process");
        }
    }
    
    private string CreateErrorFixSuccessMessage(bool success)
    {
        Debug.Log($"[ChatWindowErrorHandler] CreateErrorFixSuccessMessage called with success: {success}");
        
        // Remove analyzing message if it still exists
        removeMessageCallback(currentAnalyzingMessage, "Analyzing");
        currentAnalyzingMessage = null;
        
        if (success)
        {
            // Check if we have recent errors to determine if we need to retry
            var consoleCapture = parentWindow.GetConsoleCapture();
            bool hasRecentErrors = consoleCapture?.HasRecentErrors() ?? false;
            
            if (!hasRecentErrors)
            {
                // Success! No more errors - complete the error fixing cycle
                var message = CreateSuccessMessage();
                HandleErrorFixingCompleted(true);
                return message;
            }
            else if (errorFixAttempts < MAX_ERROR_FIX_ATTEMPTS)
            {
                // Still have errors, try again
                Debug.Log("[ChatWindowErrorHandler] Still have errors, attempting another fix");
                
                // Schedule another attempt (note: we can pass null for suggestionSystem and messages since they're only used for UI updates)
                EditorApplication.delayCall += () =>
                {
                    AttemptBatchErrorFix(currentErrorBatch, null, null);
                };
                
                return "❗ Some errors remain, attempting additional fixes...";
            }
            else
            {
                // Max attempts reached
                HandleErrorFixingCompleted(false);
                return $"⚠️ Error fixing completed after {MAX_ERROR_FIX_ATTEMPTS} attempts. Some errors may still remain.";
            }
        }
        else
        {
            // Compilation failed
            HandleErrorFixingCompleted(false);
            return "❌ Compilation failed. Please check the console for errors.";
        }
    }
    
    private string CreateSuccessMessage()
    {
        if (fixSummary.Count > 0)
        {
            var summary = new StringBuilder();
            summary.AppendLine("✅ Scripts compiled successfully!");
            summary.AppendLine();
            summary.AppendLine("✅ **Error fixing completed successfully!**");
            summary.AppendLine();
            summary.AppendLine("**Summary of fixes applied:**");
            
            for (int i = 0; i < fixSummary.Count; i++)
            {
                summary.AppendLine($"{i + 1}. {fixSummary[i]}");
            }
            
            summary.AppendLine();
            summary.AppendLine("All errors have been resolved. Your scripts should now compile without issues!");
            return summary.ToString();
        }
        else
        {
            return "✅ Scripts compiled successfully!\n\n✅ Error fixing completed successfully! All errors have been resolved.";
        }
    }
    
    private async void AttemptBatchErrorFix(List<ErrorBatch> errorBatch, ChatSuggestionSystem suggestionSystem, List<ChatMessage> messages)
    {
        Debug.Log($"[ChatWindowErrorHandler] AttemptBatchErrorFix started, attempt {errorFixAttempts + 1}/{MAX_ERROR_FIX_ATTEMPTS}");
        
        if (isWaitingForAICallback()) 
        {
            Debug.Log("[ChatWindowErrorHandler] Already waiting for AI, this should not happen with queue system");
            return;
        }
        
        try
        {
            Debug.Log("[ChatWindowErrorHandler] Setting isWaitingForAI to true and starting error fix");
            setWaitingForAICallback(true);
            isInErrorFixingCycle = true;
            currentErrorBatch = errorBatch;
            errorFixAttempts++;
            
            var errorReport = BuildBatchErrorReport(errorBatch);
            
            string analysisMessage = errorFixAttempts == 1 ? 
                "🔧 Analyzing errors with AI..." : 
                $"🔧 Analyzing remaining errors (attempt {errorFixAttempts}/{MAX_ERROR_FIX_ATTEMPTS})...";
                
            currentAnalyzingMessage = new ChatMessage("Claude", "", MessageType.Normal);
            addMessageCallback(currentAnalyzingMessage);
            scrollToBottomCallback();
            repaintCallback();
            
            Debug.Log("[ChatWindowErrorHandler] Sending error report to Claude AI with streaming");
            
            // Create a separate conversation history for error fixing
            var errorFixHistory = new List<ClaudeMessage>();
            
            string aiResponse = await ClaudeAIAgent.SendMessageStreamAsync(
                errorReport.ToString(), 
                errorFixHistory, 
                isErrorFix: true,
                OnErrorFixStreamingTextDelta
            );
            
            Debug.Log($"[ChatWindowErrorHandler] Received AI response: {aiResponse?.Substring(0, Math.Min(100, aiResponse?.Length ?? 0))}...");
            
            if (!string.IsNullOrEmpty(aiResponse))
            {
                // Extract fix summary from AI response
                ExtractFixSummary(aiResponse);
                
                // Update the final message
                currentAnalyzingMessage.message = aiResponse;
                Debug.Log("[ChatWindowErrorHandler] Updated Claude's error fix response");
                
                // Register our success callback with the main window's unified compilation system
                parentWindow.RegisterCompilationSuccessCallback(CreateErrorFixSuccessMessage);
                
                // Reset the waiting state immediately after adding the response
                // The unified compilation tracking will handle the rest
                setWaitingForAICallback(false);
                
                // If no compilation happens within a reasonable time, reset state
                EditorApplication.delayCall += () =>
                {
                    EditorApplication.delayCall += () =>
                    {
                        if (!EditorApplication.isCompiling)
                        {
                            Debug.Log("[ChatWindowErrorHandler] No compilation detected after error fix attempt, resetting state");
                            // No compilation happened, so no actual fixes were made
                            
                            // Clear the callback and reset state
                            parentWindow.ClearCompilationSuccessCallback();
                            HandleErrorFixingCompleted(false);
                        }
                    };
                };
            }
            else
            {
                removeMessageCallback(currentAnalyzingMessage, "Analyzing");
                currentAnalyzingMessage = null;
                addMessageCallback(new ChatMessage("System", "❌ Error: Claude AI returned empty response", MessageType.Error));
                
                // Reset state since we're not continuing
                HandleErrorFixingCompleted(false);
            }
            
            suggestionSystem.UpdateSuggestions(true, messages);
            scrollToBottomCallback();
            repaintCallback();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ChatWindowErrorHandler] Error fix failed: {ex.Message}\nStack trace: {ex.StackTrace}");
            
            removeMessageCallback(currentAnalyzingMessage, "Analyzing");
            currentAnalyzingMessage = null;
            
            addMessageCallback(new ChatMessage("System", $"❌ Error fix failed: {ex.Message}", MessageType.Error));
            
            // Reset state since we're not continuing
            HandleErrorFixingCompleted(false);
            
            scrollToBottomCallback();
            repaintCallback();
        }
    }
    
    private void OnErrorFixStreamingTextDelta(string textDelta)
    {
        if (currentAnalyzingMessage != null)
        {
            currentAnalyzingMessage.message += textDelta;
            
            // Update UI on main thread
            EditorApplication.delayCall += () => {
                scrollToBottomCallback();
                repaintCallback();
            };
        }
    }
    
    private void HandleErrorFixingCompleted(bool success)
    {
        Debug.Log($"[ChatWindowErrorHandler] Error fixing completed. Success: {success}");
        
        // Reset error fixing state
        currentErrorBatch = null;
        errorFixAttempts = 0;
        fixSummary.Clear();
        isInErrorFixingCycle = false;
        
        scrollToBottomCallback();
        repaintCallback();
        
        // Notify parent window
        OnErrorFixingCompleted?.Invoke(success);
    }
    
    private string CreateErrorSummary(List<ErrorBatch> errorBatch)
    {
        if (errorBatch.Count == 1)
        {
            var error = errorBatch[0];
            var countText = error.Count > 1 ? $" (occurred {error.Count} times)" : "";
            return $"Error detected{countText}: {error.LogString} - attempting automatic fix...";
        }
        else
        {
            var totalErrors = errorBatch.Sum(e => e.Count);
            return $"Multiple errors detected ({errorBatch.Count} unique errors, {totalErrors} total occurrences) - attempting automatic fix...";
        }
    }
    
    private void ExtractFixSummary(string aiResponse)
    {
        var lines = aiResponse.Split('\n');
        
        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();
            
            // Look for lines that describe fixes
            if (trimmedLine.StartsWith("Fixed:") || 
                trimmedLine.StartsWith("Corrected:") ||
                trimmedLine.StartsWith("Resolved:") ||
                trimmedLine.Contains("was missing") ||
                trimmedLine.Contains("was incorrect") ||
                (trimmedLine.Contains("Fixed") && trimmedLine.Contains("error")))
            {
                fixSummary.Add(trimmedLine);
            }
        }
    }
    
    private StringBuilder BuildBatchErrorReport(List<ErrorBatch> errorBatch)
    {
        var errorReport = new StringBuilder();
        errorReport.AppendLine("UNITY ERRORS DETECTED - Please analyze and fix:");
        errorReport.AppendLine();
        
        for (int i = 0; i < errorBatch.Count; i++)
        {
            var error = errorBatch[i];
            errorReport.AppendLine($"ERROR #{i + 1}:");
            errorReport.AppendLine($"Type: {error.LogType}");
            errorReport.AppendLine($"Message: {error.LogString}");
            
            if (error.Count > 1)
            {
                errorReport.AppendLine($"Occurrences: {error.Count}");
            }
            
            if (!string.IsNullOrEmpty(error.StackTrace))
            {
                errorReport.AppendLine($"Stack Trace: {error.StackTrace}");
            }
            
            errorReport.AppendLine();
        }
        
        errorReport.AppendLine("Please:");
        errorReport.AppendLine("1. Analyze all errors to identify problematic scripts and root causes");
        errorReport.AppendLine("2. Use read_script to examine problematic scripts identified from stack traces");
        errorReport.AppendLine("3. Use edit_script to fix the issues");
        errorReport.AppendLine("4. Explain what was wrong and how you fixed it");
        errorReport.AppendLine("5. If multiple errors are related, fix them together for efficiency");
        
        return errorReport;
    }
} 