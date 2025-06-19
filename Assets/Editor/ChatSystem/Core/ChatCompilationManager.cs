using UnityEngine;
using UnityEditor;
using UnityEditor.Compilation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public class ChatCompilationManager
{
    // Event-based compilation tracking (persisted across domain reloads)
    private static readonly string SHOULD_NOTIFY_CLAUDE_KEY = "ChatWindow_ShouldNotifyClaude";
    private static readonly string CONVERSATION_HISTORY_KEY = "ChatWindow_ConversationHistory";
    private static readonly string COMPILATION_MESSAGE_KEY = "ChatWindow_CompilationMessage";
    
    // Instance state
    private ChatMessage currentCompilationWaitMessage = null;
    private Func<bool, string> customSuccessMessageProvider = null;
    private bool compilationEventsSetup = false;
    private double lastCompilationResultTime = 0;
    
    // Static state for Claude script operations
    private static bool claudePerformingScriptOperation = false;
    private static double lastClaudeScriptOperationTime = 0;
    
    // References to parent components
    private ChatWindow parentWindow;
    private ChatMessageStreamingManager streamingManager;
    private List<ClaudeMessage> conversationHistory;
    
    // Events
    public event Action<string> OnSystemMessage;
    public event Action OnRepaint;
    
    public ChatCompilationManager(ChatWindow parent, ChatMessageStreamingManager streaming, List<ClaudeMessage> conversationHistoryList)
    {
        parentWindow = parent;
        streamingManager = streaming;
        conversationHistory = conversationHistoryList;
        
        SetupInstanceCompilationEvents();
    }
    
    public void Cleanup()
    {
        CleanupInstanceCompilationEvents();
    }
    
    public void RegisterCompilationSuccessCallback(Func<bool, string> successMessageProvider)
    {
        customSuccessMessageProvider = successMessageProvider;
    }
    
    public void ClearCompilationSuccessCallback()
    {
        customSuccessMessageProvider = null;
    }
    
    public void CheckForMissedCompilationResult()
    {
        try
        {
            bool shouldNotify = SessionState.GetBool(SHOULD_NOTIFY_CLAUDE_KEY, false);
            string compilationMessage = SessionState.GetString(COMPILATION_MESSAGE_KEY, "");
            
            if (shouldNotify && !string.IsNullOrEmpty(compilationMessage))
            {
                Debug.Log($"[ChatCompilationManager] Found missed compilation result: {compilationMessage}");
                OnSystemMessage?.Invoke($"🔧 DEBUG: Found saved compilation result: {compilationMessage}");
                
                // Process the missed compilation result
                EditorApplication.delayCall += () => CheckCompilationResultsAndNotifyClaude();
            }
            else if (shouldNotify)
            {
                OnSystemMessage?.Invoke("🔧 DEBUG: SHOULD_NOTIFY_CLAUDE_KEY was TRUE but no compilation message saved");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ChatCompilationManager] Exception in CheckForMissedCompilationResult: {ex.Message}");
        }
    }
    
    private void SetupInstanceCompilationEvents()
    {
        if (compilationEventsSetup) return;
        
        try
        {
            Debug.Log("[ChatCompilationManager] Setting up compilation events");
            
            CompilationPipeline.compilationStarted += OnCompilationStarted;
            CompilationPipeline.compilationFinished += OnCompilationFinished;
            CompilationPipeline.assemblyCompilationFinished += OnAssemblyCompilationFinished;
            
            compilationEventsSetup = true;
            Debug.Log("[ChatCompilationManager] Compilation events set up successfully");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ChatCompilationManager] Exception setting up compilation events: {ex.Message}");
        }
    }
    
    private void CleanupInstanceCompilationEvents()
    {
        if (!compilationEventsSetup) return;
        
        try
        {
            Debug.Log("[ChatCompilationManager] Cleaning up compilation events");
            
            CompilationPipeline.compilationStarted -= OnCompilationStarted;
            CompilationPipeline.compilationFinished -= OnCompilationFinished;
            CompilationPipeline.assemblyCompilationFinished -= OnAssemblyCompilationFinished;
            
            compilationEventsSetup = false;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ChatCompilationManager] Exception cleaning up compilation events: {ex.Message}");
        }
    }
    
    private void OnCompilationStarted(object obj)
    {
        try
        {
            Debug.Log("[ChatCompilationManager] OnCompilationStarted triggered");
            OnSystemMessage?.Invoke("🔧 DEBUG: Compilation started");
            
            // Check if this compilation should notify Claude
            bool shouldRespond = ShouldRespondToCompilation();
            OnSystemMessage?.Invoke($"🔧 DEBUG: Should respond to compilation: {shouldRespond}");
            
            if (shouldRespond)
            {
                Debug.Log("[ChatCompilationManager] This compilation should notify Claude - setting flag");
                SessionState.SetBool(SHOULD_NOTIFY_CLAUDE_KEY, true);
                OnSystemMessage?.Invoke("🔧 DEBUG: Set SHOULD_NOTIFY_CLAUDE_KEY to TRUE");
                
                // Add or update the "waiting for compilation" message
                if (currentCompilationWaitMessage == null)
                {
                    currentCompilationWaitMessage = ChatMessage.CreateSystemMessage("⏳ Compiling scripts...", MessageType.System);
                    streamingManager.QueueMessage(currentCompilationWaitMessage, true);
                }
                else
                {
                    currentCompilationWaitMessage.content = "⏳ Compiling scripts...";
                    currentCompilationWaitMessage.isComplete = false;
                }
                
                OnRepaint?.Invoke();
            }
            else
            {
                Debug.Log("[ChatCompilationManager] This compilation will not notify Claude");
                OnSystemMessage?.Invoke("🔧 DEBUG: This compilation will not notify Claude - no flag set");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ChatCompilationManager] Exception in OnCompilationStarted: {ex.Message}\n{ex.StackTrace}");
            OnSystemMessage?.Invoke($"EXCEPTION in OnCompilationStarted: {ex.Message}");
        }
    }
    
    private void OnCompilationFinished(object obj)
    {
        try
        {
            Debug.Log("[ChatCompilationManager] OnCompilationFinished triggered");
            OnSystemMessage?.Invoke("🔧 DEBUG: Compilation finished");
            
            // Always update the compilation wait message to show completion
            if (currentCompilationWaitMessage != null)
            {
                currentCompilationWaitMessage.content = "✓ Compilation completed";
                currentCompilationWaitMessage.isComplete = true;
                OnRepaint?.Invoke();
            }
            
            // Check if we should notify Claude about this compilation
            bool shouldNotify = SessionState.GetBool(SHOULD_NOTIFY_CLAUDE_KEY, false);
            OnSystemMessage?.Invoke($"🔧 DEBUG: SHOULD_NOTIFY_CLAUDE_KEY = {shouldNotify}");
            
            if (shouldNotify)
            {
                OnSystemMessage?.Invoke("🔧 DEBUG: Will check compilation results and notify Claude");
                
                // Schedule compilation result processing with a small delay
                EditorApplication.delayCall += () => {
                    EditorApplication.delayCall += () => {
                        CheckCompilationResultsAndNotifyClaude();
                    };
                };
            }
            else
            {
                OnSystemMessage?.Invoke("🔧 DEBUG: No Claude notification needed - flag was FALSE");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ChatCompilationManager] Exception in OnCompilationFinished: {ex.Message}\n{ex.StackTrace}");
            OnSystemMessage?.Invoke($"EXCEPTION in OnCompilationFinished: {ex.Message}");
        }
    }
    
    private void OnAssemblyCompilationFinished(string assemblyPath, UnityEditor.Compilation.CompilerMessage[] messages)
    {
        OnSystemMessage?.Invoke($"Assembly compilation finished: {System.IO.Path.GetFileName(assemblyPath)}");
        Debug.Log($"[ChatCompilationManager] Assembly compilation finished: {System.IO.Path.GetFileName(assemblyPath)}");
        
        // Let the global compilation handler process the results
        bool shouldNotify = SessionState.GetBool(SHOULD_NOTIFY_CLAUDE_KEY, false);
        if (shouldNotify)
        {
            OnSystemMessage?.Invoke("Assembly compilation finished - waiting for global compilation to complete");
        }
    }
    
    private void CheckCompilationResultsAndNotifyClaude()
    {
        try
        {
            Debug.Log("[ChatCompilationManager] CheckCompilationResultsAndNotifyClaude called");
            OnSystemMessage?.Invoke("🔧 DEBUG: CheckCompilationResultsAndNotifyClaude called");
            
            // Check if we should notify Claude
            bool shouldNotify = SessionState.GetBool(SHOULD_NOTIFY_CLAUDE_KEY, false);
            if (!shouldNotify)
            {
                Debug.Log("[ChatCompilationManager] No Claude notification needed");
                OnSystemMessage?.Invoke("🔧 DEBUG: No Claude notification needed - flag is false");
                return;
            }
            
            OnSystemMessage?.Invoke("🔧 DEBUG: Checking compilation results...");
            
            // Clear the flag immediately to prevent duplicate notifications
            SessionState.SetBool(SHOULD_NOTIFY_CLAUDE_KEY, false);
            SessionState.EraseString(COMPILATION_MESSAGE_KEY);
            OnSystemMessage?.Invoke("🔧 DEBUG: Cleared notification flag");
            
            // Get compilation messages
            var messages = CompilationPipeline.GetCompilerMessages();
            bool hasErrors = messages.Any(m => m.type == UnityEditor.Compilation.CompilerMessageType.Error);
            bool hasWarnings = messages.Any(m => m.type == UnityEditor.Compilation.CompilerMessageType.Warning);
            
            OnSystemMessage?.Invoke($"🔧 DEBUG: Found {messages.Length} messages, {messages.Count(m => m.type == UnityEditor.Compilation.CompilerMessageType.Error)} errors, {messages.Count(m => m.type == UnityEditor.Compilation.CompilerMessageType.Warning)} warnings");
            
            string compilationMessage;
            bool success = !hasErrors;
            
            if (success)
            {
                // Use custom success message if available
                if (customSuccessMessageProvider != null)
                {
                    compilationMessage = customSuccessMessageProvider(hasWarnings);
                    OnSystemMessage?.Invoke("🔧 DEBUG: Using custom success message");
                }
                else
                {
                    compilationMessage = hasWarnings ? 
                        "✅ Scripts compiled successfully with warnings." : 
                        "✅ Scripts compiled successfully!";
                }
            }
            else
            {
                var errorMessages = messages
                    .Where(m => m.type == UnityEditor.Compilation.CompilerMessageType.Error)
                    .Take(5) // Limit to first 5 errors
                    .Select(m => $"• {m.file}({m.line}): {m.message}")
                    .ToList();
                
                compilationMessage = $"❌ Compilation failed with {messages.Count(m => m.type == UnityEditor.Compilation.CompilerMessageType.Error)} error(s):\n{string.Join("\n", errorMessages)}";
                
                if (messages.Count(m => m.type == UnityEditor.Compilation.CompilerMessageType.Error) > 5)
                {
                    compilationMessage += "\n... and more errors";
                }
            }
            
            OnSystemMessage?.Invoke($"🔧 DEBUG: Sending compilation result to Claude: {success}");
            
            // Update the compilation wait message with results
            if (currentCompilationWaitMessage != null)
            {
                currentCompilationWaitMessage.content = success ? "✅ Compilation successful" : "❌ Compilation failed";
                currentCompilationWaitMessage.isComplete = true;
                currentCompilationWaitMessage = null; // Clear reference
                OnRepaint?.Invoke();
            }
            
            // Send result to Claude
            SendCompilationResultToClaude(compilationMessage, success);
            
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ChatCompilationManager] Exception in CheckCompilationResultsAndNotifyClaude: {ex.Message}\n{ex.StackTrace}");
            OnSystemMessage?.Invoke($"EXCEPTION in CheckCompilationResultsAndNotifyClaude: {ex.Message}");
        }
    }
    
    private async void SendCompilationResultToClaude(string compilationMessage, bool success)
    {
        try
        {
            OnSystemMessage?.Invoke("🔧 DEBUG: SendCompilationResultToClaude started");
            
            // Save compilation message to session state for recovery
            SessionState.SetString(COMPILATION_MESSAGE_KEY, compilationMessage);
            
            // Add compilation result as a user message to conversation history
            conversationHistory.Add(ClaudeMessage.CreateTextMessage("user", 
                $"[COMPILATION RESULT] {compilationMessage}"));
            
            OnSystemMessage?.Invoke("🔧 DEBUG: Added compilation result to conversation history");
            
            // Wait a moment for any ongoing streaming to complete
            await Task.Delay(500);
            
            // Process the compilation result response
            await ProcessCompilationResultResponse();
            
            OnSystemMessage?.Invoke("🔧 DEBUG: SendCompilationResultToClaude completed");
            
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ChatCompilationManager] Error in SendCompilationResultToClaude: {ex.Message}");
            OnSystemMessage?.Invoke($"ERROR in SendCompilationResultToClaude: {ex.Message}");
            
            // Show error message to user
            var errorMessage = ChatMessage.CreateSystemMessage($"Failed to send compilation result to Claude: {ex.Message}", MessageType.Error);
            streamingManager.QueueMessage(errorMessage);
        }
    }
    
    private async Task ProcessCompilationResultResponse()
    {
        try
        {
            OnSystemMessage?.Invoke("🔧 DEBUG: ProcessCompilationResultResponse started");
            
            // Create AI response message for streaming
            var aiMessage = ChatMessage.CreateAssistantMessage("", shouldStream: true);
            streamingManager.QueueMessage(aiMessage);
            
            // Send to Claude AI
            string aiResponse = await ClaudeAIAgent.SendMessageStreamAsync("", conversationHistory,
                (textDelta) => streamingManager.OnUnifiedStreamingTextDelta(aiMessage, textDelta));
            
            // Mark as complete
            aiMessage.isComplete = true;
            
            // Add AI response to conversation history
            conversationHistory.Add(ClaudeMessage.CreateTextMessage("assistant", aiResponse));
            
            OnSystemMessage?.Invoke("🔧 DEBUG: ProcessCompilationResultResponse completed successfully");
            
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ChatCompilationManager] Error in ProcessCompilationResultResponse: {ex.Message}");
            OnSystemMessage?.Invoke($"ERROR in ProcessCompilationResultResponse: {ex.Message}");
        }
    }
    
    // Static methods for tools to notify about script operations
    public static void NotifyClaudeScriptOperationStarted()
    {
        claudePerformingScriptOperation = true;
        lastClaudeScriptOperationTime = EditorApplication.timeSinceStartup;
        Debug.Log("[ChatCompilationManager] Claude script operation started - compilation tracking enabled");
    }
    
    public static void NotifyClaudeScriptOperationCompleted()
    {
        // Keep the flag active for a short time to catch delayed compilations
        EditorApplication.delayCall += () => {
            EditorApplication.delayCall += () => {
                claudePerformingScriptOperation = false;
                Debug.Log("[ChatCompilationManager] Claude script operation completed - compilation tracking disabled");
            };
        };
    }
    
    public static bool ShouldRespondToCompilation()
    {
        const double SCRIPT_OPERATION_TIMEOUT = 10.0; // 10 seconds
        double timeSinceLastOperation = EditorApplication.timeSinceStartup - lastClaudeScriptOperationTime;
        
        bool shouldRespond = claudePerformingScriptOperation || timeSinceLastOperation < SCRIPT_OPERATION_TIMEOUT;
        Debug.Log($"[ChatCompilationManager] ShouldRespondToCompilation: {shouldRespond} (flag: {claudePerformingScriptOperation}, timeSince: {timeSinceLastOperation:F1}s)");
        return shouldRespond;
    }
} 