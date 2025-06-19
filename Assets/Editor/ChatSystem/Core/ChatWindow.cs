using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using Newtonsoft.Json;

public class ChatWindow : EditorWindow
{
    // Core data
    private List<ChatMessage> messages = new List<ChatMessage>();
    private List<ClaudeMessage> conversationHistory = new List<ClaudeMessage>();
    private string inputMessage = "";
    private string currentUsername = "User";
    private bool aiEnabled = true;
    private bool isWaitingForAI = false;
    private bool autoFixEnabled = true;
    private bool autoCompilationEnabled = true;
    private bool showTokenUsage = false;
    
    // Component managers (extracted functionality)
    private ChatWindowUI windowUI;
    private ChatMessageStreamingManager streamingManager;
    private ChatCompilationManager compilationManager;
    private ChatConsoleCapture consoleCapture;
    private ChatCommandHandler commandHandler;
    private ChatWindowErrorHandler errorHandler;
    
    // Static initialization to ensure compilation events are always ready
    [InitializeOnLoadMethod]
    private static void InitializeCompilationEvents()
    {
        EditorApplication.delayCall += () =>
        {
            var chatWindows = Resources.FindObjectsOfTypeAll<ChatWindow>();
            if (chatWindows.Length > 0)
            {
                foreach (var window in chatWindows)
                {
                    window.EnsureComponentsInitialized();
                }
            }
        };
    }
    
    [MenuItem("Tools/Chat Window %#d")]
    public static void ShowWindow()
    {
        ChatWindow window = GetWindow<ChatWindow>("Chat Window");
        window.minSize = new Vector2(400, 600);
        window.Show();
    }
    
    private void OnEnable()
    {
        InitializeComponents();
        SetupWelcomeMessages();
        SetupEventHandlers();
        
        consoleCapture.StartCapturing();
        ChatWindowCLI.StartMonitoring();
        
        // Check for missed compilation results
        compilationManager.CheckForMissedCompilationResult();
    }
    
    private void OnDisable()
    {
        ChatWindowCLI.StopMonitoring();
        
        // Ensure compilation is re-enabled when window closes
        if (!autoCompilationEnabled)
        {
            EditorApplication.UnlockReloadAssemblies();
            Debug.Log("[ChatWindow] Re-enabled automatic compilation on window close");
        }
        
        CleanupComponents();
    }
    
    private void OnGUI()
    {
        EnsureComponentsInitialized();
        
        windowUI.DrawUI(messages, ref inputMessage, aiEnabled, isWaitingForAI, 
                       streamingManager.CurrentlyStreamingMessage, autoFixEnabled, 
                       autoCompilationEnabled, showTokenUsage, position);
    }
    
    #region Component Management
    
    private void EnsureComponentsInitialized()
    {
        if (windowUI == null || streamingManager == null || compilationManager == null)
        {
            InitializeComponents();
        }
    }
    
    private void InitializeComponents()
    {
        // Initialize UI manager
        windowUI = new ChatWindowUI(this);
        
        // Initialize streaming manager
        streamingManager = new ChatMessageStreamingManager(this, messages, conversationHistory);
        
        // Initialize compilation manager
        compilationManager = new ChatCompilationManager(this, streamingManager, conversationHistory);
        
        // Initialize other components
        consoleCapture = new ChatConsoleCapture();
        commandHandler = new ChatCommandHandler();
        errorHandler = new ChatWindowErrorHandler(this);
        
        // Setup event handlers
        SetupStreamingManagerEvents();
        SetupCompilationManagerEvents();
        SetupErrorHandlerEvents();
        
        consoleCapture.StartCapturing();
        windowUI.UpdateSuggestions(aiEnabled, messages, isWaitingForAI || streamingManager.CurrentlyStreamingMessage != null);
    }
    
    private void SetupStreamingManagerEvents()
    {
        if (streamingManager != null)
        {
            streamingManager.OnMessageProcessingComplete += () =>
            {
                isWaitingForAI = false;
                windowUI.UpdateSuggestions(aiEnabled, messages, false);
                Repaint();
            };
            
            streamingManager.OnScrollToBottom += () => windowUI.ScrollToBottom();
            streamingManager.OnRepaint += Repaint;
        }
    }
    
    private void SetupCompilationManagerEvents()
    {
        if (compilationManager != null)
        {
            compilationManager.OnSystemMessage += (message) => windowUI.UpdateSystemMessage(message);
            compilationManager.OnRepaint += Repaint;
        }
    }
    
    private void SetupErrorHandlerEvents()
    {
        if (errorHandler != null)
        {
            errorHandler.Initialize(
                (message) => streamingManager.QueueMessage(message, message.type == MessageType.System || message.type == MessageType.Error),
                null,
                () => isWaitingForAI,
                (value) => isWaitingForAI = value,
                () => windowUI.ScrollToBottom(),
                Repaint
            );
            
            errorHandler.OnErrorFixingCompleted += OnErrorFixingCompleted;
        }
    }
    
    private void SetupWelcomeMessages()
    {
        if (messages.Count == 0)
        {
            string welcomeMessage = "Welcome to Unity Chat Window with Claude AI! " +
                                  "AI is enabled by default - ask Claude to create scripts, GameObjects, or help with Unity tasks. " +
                                  "Auto Fix is enabled by default to automatically fix compilation errors. " +
                                  "Example: 'Create a player movement script' or 'Create a red cube at position 0,5,0'. " +
                                  "Type /help for available commands.";
            
            windowUI.UpdateSystemMessage(welcomeMessage);
        }
    }
    
    private void SetupEventHandlers()
    {
        if (consoleCapture != null)
        {
            consoleCapture.OnErrorBatchReceived += OnErrorBatchReceived;
        }
        
        if (commandHandler != null)
        {
            commandHandler.OnClearRequested += ClearMessages;
            commandHandler.OnCopyRequested += CopyConversationToClipboard;
            commandHandler.OnMessageAdded += (message) => streamingManager.AddMessage(message);
        }
    }
    
    private void CleanupComponents()
    {
        if (consoleCapture != null)
        {
            consoleCapture.OnErrorBatchReceived -= OnErrorBatchReceived;
            consoleCapture.StopCapturing();
        }
        
        if (commandHandler != null)
        {
            commandHandler.OnClearRequested -= ClearMessages;
            commandHandler.OnCopyRequested -= CopyConversationToClipboard;
        }
        
        if (errorHandler != null)
        {
            errorHandler.OnErrorFixingCompleted -= OnErrorFixingCompleted;
        }
        
        if (compilationManager != null)
        {
            compilationManager.Cleanup();
        }
    }
    
    #endregion
    
    #region Public Interface for UI Components
    
    public async void SendMessage(string message)
    {
        if (string.IsNullOrEmpty(message.Trim())) return;
        
        inputMessage = "";
        GUIUtility.keyboardControl = 0;
        Repaint();
        
        var userMessage = ChatMessage.CreateUserMessage(currentUsername, message);
        streamingManager.AddMessage(userMessage);
        conversationHistory.Add(ClaudeMessage.CreateTextMessage("user", message));
        
        // Handle commands
        if (message.StartsWith("/"))
        {
            commandHandler.HandleCommand(message);
            windowUI.UpdateSuggestions(aiEnabled, messages, isWaitingForAI || streamingManager.CurrentlyStreamingMessage != null);
            return;
        }
        
        // Send to AI if enabled
        if (aiEnabled)
        {
            isWaitingForAI = true;
            await streamingManager.ProcessAIResponse(message);
        }
        else
        {
            var systemMessage = ChatMessage.CreateSystemMessage("AI is currently disabled.", MessageType.System);
            streamingManager.AddMessage(systemMessage);
        }
        
        windowUI.UpdateSuggestions(aiEnabled, messages, isWaitingForAI || streamingManager.CurrentlyStreamingMessage != null);
    }
    
    public void SendSuggestion(string suggestion)
    {
        if (!string.IsNullOrEmpty(suggestion))
        {
            inputMessage = suggestion;
            Repaint();
        }
    }
    
    public void StopStreaming()
    {
        streamingManager.StopStreaming();
    }
    
    public void OnAutoFixToggled(bool enabled, string statusMessage)
    {
        autoFixEnabled = enabled;
        windowUI.UpdateSystemMessage(statusMessage);
    }
    
    public void OnAutoCompilationToggled(bool enabled)
    {
        autoCompilationEnabled = enabled;
        
        if (enabled)
        {
            if (EditorApplication.isCompiling)
            {
                windowUI.UpdateSystemMessage("🔄 Auto Compilation enabled - current compilation will finish");
            }
            else
            {
                EditorApplication.UnlockReloadAssemblies();
                windowUI.UpdateSystemMessage("✅ Auto Compilation enabled - scripts will compile automatically when modified");
            }
        }
        else
        {
            EditorApplication.LockReloadAssemblies();
            windowUI.UpdateSystemMessage("⏸️ Auto Compilation disabled - scripts will not automatically compile until re-enabled");
        }
    }
    
    public void OnTokenUsageToggled(bool enabled, string statusMessage)
    {
        showTokenUsage = enabled;
        windowUI.UpdateSystemMessage(statusMessage);
        Repaint();
    }
    
    public void CopyConversationToClipboard()
    {
        var conversation = new StringBuilder();
        foreach (var message in messages)
        {
            conversation.AppendLine($"[{message.timestamp:HH:mm:ss}] {message.sender}: {message.content}");
        }
        
        EditorGUIUtility.systemCopyBuffer = conversation.ToString();
        windowUI.UpdateSystemMessage("📋 Conversation copied to clipboard!");
    }
    
    public void ClearMessages()
    {
        messages.Clear();
        conversationHistory.Clear();
        windowUI.UpdateSystemMessage("🗑️ Chat cleared");
        SetupWelcomeMessages();
        Repaint();
    }
    
    #endregion
    
    #region Legacy Interface for Tools and Components
    
    public ChatConsoleCapture GetConsoleCapture() => consoleCapture;
    
    public void RegisterCompilationSuccessCallback(Func<bool, string> successMessageProvider)
    {
        compilationManager?.RegisterCompilationSuccessCallback(successMessageProvider);
    }
    
    public void ClearCompilationSuccessCallback()
    {
        compilationManager?.ClearCompilationSuccessCallback();
    }
    
    // Static methods for tool integration
    public static void SendDebugMessage(string message)
    {
        var chatWindows = Resources.FindObjectsOfTypeAll<ChatWindow>();
        if (chatWindows.Length > 0)
        {
            chatWindows[0].windowUI?.UpdateSystemMessage($"🔧 DEBUG: {message}");
        }
        else
        {
            Debug.Log($"[ChatWindow] DEBUG (no window): {message}");
        }
    }
    
    public static void NotifyClaudeScriptOperationStarted()
    {
        ChatCompilationManager.NotifyClaudeScriptOperationStarted();
    }
    
    public static void NotifyClaudeScriptOperationCompleted()
    {
        ChatCompilationManager.NotifyClaudeScriptOperationCompleted();
    }
    
    #endregion
    
    #region Event Handlers
    
    private void OnErrorBatchReceived(List<ErrorBatch> errorBatch)
    {
        errorHandler?.OnErrorBatchReceived(errorBatch, aiEnabled, consoleCapture, windowUI.GetSuggestionSystem(), messages);
    }
    
    private void OnErrorFixingCompleted(bool success)
    {
        isWaitingForAI = false;
        windowUI.UpdateSuggestions(aiEnabled, messages, false);
        
        string message = success ? 
            "✅ Error fixing completed successfully!" : 
            "⚠️ Error fixing completed with some issues remaining.";
        
        windowUI.UpdateSystemMessage(message);
        Repaint();
    }
    
    #endregion
}

// Legacy support structures
[System.Serializable]
public class SerializableMessageList
{
    public List<ChatMessage> messages;
    
    public SerializableMessageList(List<ChatMessage> messages)
    {
        this.messages = messages;
    }
} 