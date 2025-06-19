using UnityEngine;
using UnityEditor;
using UnityEditor.Compilation;
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
    private Vector2 scrollPosition;
    private bool aiEnabled = true;
    private bool isWaitingForAI = false;
    private bool autoFixEnabled = true;
    private bool autoCompilationEnabled = true;
    private bool showTokenUsage = false; // New toggle for token usage display
    
    // System message display
    private string lastSystemMessage = "";
    private double lastSystemMessageTime = 0;
    
    // Unified message streaming system
    private Queue<MessageQueueEntry> messageQueue = new Queue<MessageQueueEntry>();
    private ChatMessage currentlyStreamingMessage = null;
    private bool isProcessingQueue = false;
    
    // Event-based compilation tracking (persisted across domain reloads)
    private static readonly string SHOULD_NOTIFY_CLAUDE_KEY = "ChatWindow_ShouldNotifyClaude";
    private static readonly string CONVERSATION_HISTORY_KEY = "ChatWindow_ConversationHistory";
    private static readonly string COMPILATION_MESSAGE_KEY = "ChatWindow_CompilationMessage";
    private ChatMessage currentCompilationWaitMessage = null;
    private Func<bool, string> customSuccessMessageProvider = null;
    
    // Instance flag to track if this window has set up compilation events
    private bool compilationEventsSetup = false;
    
    // Component managers
    private ChatMessageRenderer messageRenderer;
    private ChatConsoleCapture consoleCapture;
    private ChatCommandHandler commandHandler;
    private ChatSuggestionSystem suggestionSystem;
    private ChatWindowErrorHandler errorHandler;
    
    // Streaming settings
    private bool useStreaming = true;
    
    // Compilation result tracking (simplified for event-based approach)
    private double lastCompilationResultTime = 0;
    
    // Track when Claude performs script operations to only respond to relevant compilations
    private static bool claudePerformingScriptOperation = false;
    private static double lastClaudeScriptOperationTime = 0;
    
    // Static initialization to ensure compilation events are always ready
    [InitializeOnLoadMethod]
    private static void InitializeCompilationEvents()
    {
        // This ensures compilation events are set up as soon as Unity loads
        EditorApplication.delayCall += () =>
        {
            var chatWindows = Resources.FindObjectsOfTypeAll<ChatWindow>();
            if (chatWindows.Length > 0)
            {
                foreach (var window in chatWindows)
                {
                    window.SetupInstanceCompilationEvents();
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
        // LoadChatHistory(); // DISABLED: Chat restoration turned off to prevent message duplication
        SetupWelcomeMessages();
        SetupEventHandlers();
        
        consoleCapture.StartCapturing();
        
        // Start CLI monitoring
        ChatWindowCLI.StartMonitoring();
        
        // Check if we missed a compilation result while window was closed
        CheckForMissedCompilationResult();
        
        // Set up compilation events for this window instance
        SetupInstanceCompilationEvents();
    }
    
    private void OnDisable()
    {
        // SaveChatHistory(); // DISABLED: Chat restoration turned off to prevent message duplication
        
        // Stop CLI monitoring
        ChatWindowCLI.StopMonitoring();
        
        // Ensure compilation is re-enabled when window closes
        if (!autoCompilationEnabled)
        {
            EditorApplication.UnlockReloadAssemblies();
            Debug.Log("[ChatWindow] Re-enabled automatic compilation on window close");
        }
        
        CleanupComponents();
    }
    
    private void InitializeComponents()
    {
        messageRenderer = new ChatMessageRenderer();
        consoleCapture = new ChatConsoleCapture();
        commandHandler = new ChatCommandHandler();
        suggestionSystem = new ChatSuggestionSystem();
        errorHandler = new ChatWindowErrorHandler(this);
        
        // Initialize error handler with callbacks
        errorHandler.Initialize(
            (message) => QueueMessage(message, message.type == MessageType.System || message.type == MessageType.Error),
            null, // No longer need message removal
            () => isWaitingForAI,
            (value) => isWaitingForAI = value,
            ScrollToBottom,
            Repaint
        );
        
        consoleCapture.StartCapturing();
        suggestionSystem.UpdateSuggestions(aiEnabled, messages, isWaitingForAI || currentlyStreamingMessage != null);
    }
    
    private void SetupWelcomeMessages()
    {
        if (messages.Count == 0 && string.IsNullOrEmpty(lastSystemMessage))
        {
            string welcomeMessage = "Welcome to Unity Chat Window with Claude AI! " +
                                  "AI is enabled by default - ask Claude to create scripts, GameObjects, or help with Unity tasks. " +
                                  "Auto Fix is enabled by default to automatically fix compilation errors. " +
                                  "Example: 'Create a player movement script' or 'Create a red cube at position 0,5,0'. " +
                                  "Type /help for available commands.";
            
            // Set as system message label
            UpdateSystemMessage(welcomeMessage);
        }
    }
    
    private void SetupEventHandlers()
    {
        // Console capture events
        if (consoleCapture != null)
        {
            consoleCapture.OnErrorBatchReceived += OnErrorBatchReceived;
        }
        
        // Command handler events
        if (commandHandler != null)
        {
            commandHandler.OnClearRequested += ClearMessages;
            commandHandler.OnCopyRequested += CopyConversationToClipboard;
            commandHandler.OnMessageAdded += AddMessage;
        }
        
        // Error handler events
        if (errorHandler != null)
        {
            errorHandler.OnErrorFixingCompleted += OnErrorFixingCompleted;
        }
        
        // Set up compilation events for this window instance
        SetupInstanceCompilationEvents();
    }
    
    private void CleanupComponents()
    {
        // Unsubscribe from events before disposing
        if (consoleCapture != null)
        {
            consoleCapture.OnErrorBatchReceived -= OnErrorBatchReceived;
            consoleCapture.StopCapturing();
        }
        
        if (commandHandler != null)
        {
            commandHandler.OnClearRequested -= ClearMessages;
            commandHandler.OnCopyRequested -= CopyConversationToClipboard;
            commandHandler.OnMessageAdded -= AddMessage;
        }
        
        if (errorHandler != null)
        {
            errorHandler.OnErrorFixingCompleted -= OnErrorFixingCompleted;
        }
        
        // Clean up compilation events for this window instance
        CleanupInstanceCompilationEvents();
    }
    
    // Public method for error handler to access console capture
    public ChatConsoleCapture GetConsoleCapture()
    {
        return consoleCapture;
    }
    
    // Public method for error handler to register custom success messages
    public void RegisterCompilationSuccessCallback(Func<bool, string> successMessageProvider)
    {
        customSuccessMessageProvider = successMessageProvider;
    }
    
    // Public method to clear custom success callback
    public void ClearCompilationSuccessCallback()
    {
        customSuccessMessageProvider = null;
    }
    
    // Check for missed compilation results when window opens
    private void CheckForMissedCompilationResult()
    {
        // Check if we have a pending Claude notification that was missed
        bool shouldNotify = SessionState.GetBool(SHOULD_NOTIFY_CLAUDE_KEY, false);
        string compilationMessage = SessionState.GetString(COMPILATION_MESSAGE_KEY, "");
        
        if (shouldNotify && !EditorApplication.isCompiling && !string.IsNullOrEmpty(compilationMessage))
        {
            Debug.Log($"[ChatWindow] Found missed compilation result - restoring and processing: {compilationMessage}");
            
            // Restore conversation history from SessionState
            RestoreConversationHistoryFromSessionState();
            
            // Clear the flags
            SessionState.SetBool(SHOULD_NOTIFY_CLAUDE_KEY, false);
            SessionState.SetString(COMPILATION_MESSAGE_KEY, "");
            
            // Trigger Claude response to the compilation result
            EditorApplication.delayCall += () =>
            {
                Debug.Log($"[ChatWindow] Triggering Claude response to restored compilation result");
                _ = ProcessStreamingAIResponse(""); // Empty string since message is in conversation history
            };
        }
    }
    
    // Instance method to set up compilation events for this window
    private void SetupInstanceCompilationEvents()
    {
        if (!compilationEventsSetup)
        {
            // Unsubscribe first to prevent duplicate subscriptions
            CompilationPipeline.compilationStarted -= OnCompilationStarted;
            CompilationPipeline.compilationFinished -= OnCompilationFinished;
            CompilationPipeline.assemblyCompilationFinished -= OnAssemblyCompilationFinished;
            
            // Now subscribe to both global and assembly-level events
            CompilationPipeline.compilationStarted += OnCompilationStarted;
            CompilationPipeline.compilationFinished += OnCompilationFinished;
            CompilationPipeline.assemblyCompilationFinished += OnAssemblyCompilationFinished;
            
            compilationEventsSetup = true;
            
            // Debug: Send message to chat window
            SendDebugMessageInstance("Instance compilation event handlers set up successfully");
        }
    }
    
    // Instance method to clean up compilation events for this window
    private void CleanupInstanceCompilationEvents()
    {
        if (compilationEventsSetup)
        {
            CompilationPipeline.compilationStarted -= OnCompilationStarted;
            CompilationPipeline.compilationFinished -= OnCompilationFinished;
            CompilationPipeline.assemblyCompilationFinished -= OnAssemblyCompilationFinished;
            
            compilationEventsSetup = false;
            Debug.Log("[ChatWindow] Instance compilation event handlers cleaned up");
        }
    }
    
    // Instance compilation event handlers - much simpler!
    private void OnCompilationStarted(object obj)
    {
        SendDebugMessageInstance("Compilation started (instance handler)");
        
        // Send debug info to chat window (survives compilation)
        SendDebugMessageInstance($"OnCompilationStarted (instance) - AI:{aiEnabled}, Messages:{messages.Count}");
        UpdateSystemMessage($"🔧 DEBUG: Compilation started - AI:{aiEnabled}, Messages:{messages.Count}, ConvHistory:{conversationHistory.Count}, Streaming:{currentlyStreamingMessage != null}");
        
        Debug.Log($"[ChatWindow] CompilationPipeline.compilationStarted event received - AI enabled: {aiEnabled}");
        Debug.Log($"[ChatWindow] Current message count: {messages.Count}, conversation history: {conversationHistory.Count}");
        Debug.Log($"[ChatWindow] IsWaitingForAI: {isWaitingForAI}, CurrentlyStreaming: {currentlyStreamingMessage != null}");
        
        // Only track compilation if AI is enabled AND Claude just performed a script operation
        if (aiEnabled && ShouldRespondToCompilation())
        {
            Debug.Log($"[ChatWindow] Claude script-related compilation started - setting notification flag to TRUE");
            
            // Mark that we should notify Claude when compilation finishes
            bool wasAlreadySet = SessionState.GetBool(SHOULD_NOTIFY_CLAUDE_KEY, false);
            SessionState.SetBool(SHOULD_NOTIFY_CLAUDE_KEY, true);
            Debug.Log($"[ChatWindow] SHOULD_NOTIFY_CLAUDE_KEY: was {wasAlreadySet}, now TRUE");
            
            // Verify the flag was actually set
            bool verifyFlag = SessionState.GetBool(SHOULD_NOTIFY_CLAUDE_KEY, false);
            Debug.Log($"[ChatWindow] Verification: SHOULD_NOTIFY_CLAUDE_KEY is now {verifyFlag}");
            
            // Send debug info to chat window
            SendDebugMessageInstance($"Setting SHOULD_NOTIFY_CLAUDE_KEY from {wasAlreadySet} to TRUE (verified: {verifyFlag})");
            UpdateSystemMessage($"🔧 DEBUG: Setting SHOULD_NOTIFY_CLAUDE_KEY from {wasAlreadySet} to TRUE (verified: {verifyFlag})");
            
            // Interrupt any currently streaming message by completing it first
            if (currentlyStreamingMessage != null)
            {
                Debug.Log($"[ChatWindow] Compilation started - interrupting currently streaming message: {currentlyStreamingMessage.id}");
                currentlyStreamingMessage.CompleteStreaming();
                currentlyStreamingMessage = null;
                UpdateSystemMessage($"🔧 DEBUG: Interrupted streaming message");
            }
            else
            {
                Debug.Log($"[ChatWindow] No currently streaming message to interrupt");
                UpdateSystemMessage($"🔧 DEBUG: No streaming message to interrupt");
            }
            
            // Add "Compiling. Please wait..." as a USER message that will be sent to Claude
            var compilingMessage = ChatMessage.CreateUserMessage("User", "Compiling. Please wait...");
            QueueMessage(compilingMessage, false);
            
            // Add to conversation history and save it to SessionState (survives compilation)
            conversationHistory.Add(ClaudeMessage.CreateTextMessage("user", "Compiling. Please wait..."));
            SaveConversationHistoryToSessionState();
            
            Debug.Log($"[ChatWindow] Added 'Compiling. Please wait...' user message and saved conversation history");
            
            ScrollToBottom();
            Repaint();
        }
        else
        {
            bool aiDisabled = !aiEnabled;
            bool noClaudeOperation = !ShouldRespondToCompilation();
            Debug.Log($"[ChatWindow] Compilation started but not tracked - AI disabled: {aiDisabled}, No Claude script operation: {noClaudeOperation}");
            UpdateSystemMessage($"🔧 DEBUG: Compilation ignored - AI disabled: {aiDisabled}, No Claude script operation: {noClaudeOperation}");
        }
    }
    
    private void OnCompilationFinished(object obj)
    {
        try
        {
            Debug.Log($"[ChatWindow] CompilationPipeline.compilationFinished event received");
            
            // Check if we should notify Claude about this compilation
            bool shouldNotify = SessionState.GetBool(SHOULD_NOTIFY_CLAUDE_KEY, false);
            string compilationMessage = SessionState.GetString(COMPILATION_MESSAGE_KEY, "");
            
            // Send debug with safety check for components (they might not be initialized yet after domain reload)
            if (messages != null)
            {
                SendDebugMessageInstance("Compilation finished (instance handler)");
                SendDebugMessageInstance($"OnCompilationFinished (instance) - SHOULD_NOTIFY_CLAUDE_KEY: {shouldNotify}, COMPILATION_MESSAGE: '{compilationMessage}'");
                SendDebugMessageInstance($"Components state - messages: {messages?.Count ?? 0}, conversationHistory: {conversationHistory?.Count ?? 0}");
            }
            else
            {
                Debug.Log($"[ChatWindow] Components not initialized yet, flag: {shouldNotify}, message: '{compilationMessage}'");
            }
            
            if (shouldNotify)
            {
                // Put debug info in chat window (survives compilation)
                if (messages != null)
                {
                    SendDebugMessageInstance("SHOULD_NOTIFY_CLAUDE_KEY was TRUE - processing compilation results");
                    UpdateSystemMessage($"🔧 DEBUG: Will notify Claude - clearing flag and processing results");
                }
                
                // Clear the notification flag immediately
                SessionState.SetBool(SHOULD_NOTIFY_CLAUDE_KEY, false);
                
                // Components are already initialized (we checked above), so call directly
                if (messages != null)
                {
                    SendDebugMessageInstance("Calling ProcessCompilationResults directly");
                }
                ProcessCompilationResults();
            }
            else
            {
                if (messages != null)
                {
                    SendDebugMessageInstance("SHOULD_NOTIFY_CLAUDE_KEY was FALSE - no Claude notification needed");
                    UpdateSystemMessage($"🔧 DEBUG: No Claude notification needed - flag was FALSE");
                    
                    // Check if we have a saved compilation message that needs processing
                    if (!string.IsNullOrEmpty(compilationMessage))
                    {
                        SendDebugMessageInstance($"Found saved compilation message but flag was false: '{compilationMessage}'");
                    }
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[ChatWindow] Exception in OnCompilationFinished: {ex.Message}\n{ex.StackTrace}");
            // Try to put exception info in chat window too if possible
            if (messages != null)
            {
                SendDebugMessageInstance($"EXCEPTION in OnCompilationFinished: {ex.Message}");
            }
        }
    }
    
    private void OnAssemblyCompilationFinished(string assemblyPath, UnityEditor.Compilation.CompilerMessage[] messages)
    {
        SendDebugMessageInstance($"Assembly compilation finished: {System.IO.Path.GetFileName(assemblyPath)}");
        
        Debug.Log($"[ChatWindow] Assembly compilation finished: {System.IO.Path.GetFileName(assemblyPath)}");
        
        // Just log assembly completion - let the global compilation handler process the results
        // This prevents the flag from being cleared prematurely
        bool shouldNotify = SessionState.GetBool(SHOULD_NOTIFY_CLAUDE_KEY, false);
        if (shouldNotify)
        {
            SendDebugMessageInstance("Assembly compilation finished - waiting for global compilation to complete");
        }
    }
    
    // Instance method for sending debug messages (safe to call from static handlers)
    private void SendDebugMessageInstance(string message)
    {
        UpdateSystemMessage($"🔧 DEBUG: {message}");
        
        // Also add to messages list so it persists
        var debugMessage = ChatMessage.CreateSystemMessage($"🔧 DEBUG: {message}", MessageType.System);
        messages.Add(debugMessage);
        ScrollToBottom();
        Repaint();
        
        // Also log to console
        Debug.Log($"[ChatWindow] DEBUG: {message}");
    }
    
    // Static method for tools to send debug messages that persist through compilation
    public static void SendDebugMessage(string message)
    {
        // Find the active chat window
        var chatWindows = Resources.FindObjectsOfTypeAll<ChatWindow>();
        if (chatWindows.Length > 0)
        {
            chatWindows[0].SendDebugMessageInstance(message);
        }
        else
        {
            // No window open, just log to console
            Debug.Log($"[ChatWindow] DEBUG (no window): {message}");
        }
    }
    
    // Static methods for tools to notify when Claude is performing script operations
    public static void NotifyClaudeScriptOperationStarted()
    {
        claudePerformingScriptOperation = true;
        lastClaudeScriptOperationTime = EditorApplication.timeSinceStartup;
        Debug.Log("[ChatWindow] Claude script operation started - compilation tracking enabled");
    }
    
    public static void NotifyClaudeScriptOperationCompleted()
    {
        // Keep the flag active for a short time to catch delayed compilations
        EditorApplication.delayCall += () => {
            EditorApplication.delayCall += () => {
                claudePerformingScriptOperation = false;
                Debug.Log("[ChatWindow] Claude script operation completed - compilation tracking disabled");
            };
        };
    }
    
    // Static method to check if we should respond to compilation
    public static bool ShouldRespondToCompilation()
    {
        const double SCRIPT_OPERATION_TIMEOUT = 10.0; // 10 seconds
        double timeSinceLastOperation = EditorApplication.timeSinceStartup - lastClaudeScriptOperationTime;
        
        bool shouldRespond = claudePerformingScriptOperation || timeSinceLastOperation < SCRIPT_OPERATION_TIMEOUT;
        Debug.Log($"[ChatWindow] ShouldRespondToCompilation: {shouldRespond} (flag: {claudePerformingScriptOperation}, timeSince: {timeSinceLastOperation:F1}s)");
        return shouldRespond;
    }
    
    private void OnGUI()
    {
        EditorGUILayout.BeginVertical(GUILayout.ExpandHeight(true));
        
        DrawHeader();
        
        // Calculate available height for messages area
        // Account for header (~25px), input area with suggestions (~180px), and padding
        float headerHeight = 25f;
        float baseInputHeight = 100f; // Base input area
        float suggestionsHeight = (suggestionSystem.CurrentSuggestions != null && suggestionSystem.CurrentSuggestions.Length > 0) ? 60f : 0f;
        float helpTextHeight = 40f; // Help text area
        float padding = 15f;
        
        float totalInputAreaHeight = baseInputHeight + suggestionsHeight + helpTextHeight + padding;
        float availableHeight = position.height - headerHeight - totalInputAreaHeight;
        availableHeight = Mathf.Max(availableHeight, 200f); // Minimum height
        
        DrawMessagesArea(availableHeight);
        
        DrawInputArea();
        
        EditorGUILayout.EndVertical();
    }
    
    private void DrawHeader()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        
        GUILayout.Label("Chat Window", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        
        // Add auto-fix toggle
        bool newAutoFixEnabled = GUILayout.Toggle(autoFixEnabled, "Auto Fix", EditorStyles.toolbarButton, GUILayout.Width(70));
        if (newAutoFixEnabled != autoFixEnabled)
        {
            autoFixEnabled = newAutoFixEnabled;
            string statusMessage = autoFixEnabled ? 
                "✅ Auto Fix enabled - compilation errors will be automatically fixed" : 
                "⚠️ Auto Fix disabled - compilation errors will only be detected";
            UpdateSystemMessage(statusMessage);
        }
        
        // Add auto-compilation toggle
        bool newAutoCompilationEnabled = GUILayout.Toggle(autoCompilationEnabled, "Auto Compile", EditorStyles.toolbarButton, GUILayout.Width(90));
        if (newAutoCompilationEnabled != autoCompilationEnabled)
        {
            autoCompilationEnabled = newAutoCompilationEnabled;
            
            if (autoCompilationEnabled)
            {
                // Enable automatic compilation
                if (EditorApplication.isCompiling)
                {
                    // If currently compiling, let it finish
                    UpdateSystemMessage("🔄 Auto Compilation enabled - current compilation will finish");
                }
                else
                {
                    EditorApplication.UnlockReloadAssemblies();
                    UpdateSystemMessage("✅ Auto Compilation enabled - scripts will compile automatically when modified");
                }
            }
            else
            {
                // Disable automatic compilation by locking assemblies
                EditorApplication.LockReloadAssemblies();
                UpdateSystemMessage("⏸️ Auto Compilation disabled - scripts will not automatically compile until re-enabled");
            }
        }
        
        // Add token usage toggle
        bool newShowTokenUsage = GUILayout.Toggle(showTokenUsage, "Token Usage", EditorStyles.toolbarButton, GUILayout.Width(90));
        if (newShowTokenUsage != showTokenUsage)
        {
            showTokenUsage = newShowTokenUsage;
            string statusMessage = showTokenUsage ? 
                "📊 Token usage display enabled - costs will be shown after each interaction" : 
                "📊 Token usage display disabled";
            UpdateSystemMessage(statusMessage);
            Repaint(); // Force repaint to update message display
        }
        
        // Add streaming toggle
                    // Streaming is always enabled now - show as read-only indicator
            GUI.enabled = false;
            GUILayout.Toggle(true, "Streaming ✓", EditorStyles.toolbarButton, GUILayout.Width(80));
            GUI.enabled = true;
        
        if (GUILayout.Button("Copy", EditorStyles.toolbarButton, GUILayout.Width(50)))
        {
            CopyConversationToClipboard();
        }
        
        if (GUILayout.Button("Clear", EditorStyles.toolbarButton, GUILayout.Width(50)))
        {
            ClearMessages();
        }
        
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space();
    }
    
    private void DrawMessagesArea(float availableHeight)
    {
        // Use the calculated available height to ensure messages area fills remaining space
        EditorGUILayout.BeginVertical(GUILayout.Height(availableHeight), GUILayout.ExpandWidth(true));
        messageRenderer.DrawMessagesArea(messages, ref scrollPosition);
        EditorGUILayout.EndVertical();
    }
    
    private void DrawInputArea()
    {
        // Fixed-height input area to prevent expansion
        EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
        
        EditorGUILayout.Space(2);
        
        // Draw system message label if we have one
        DrawSystemMessageLabel();
        
        EditorGUILayout.BeginHorizontal();
        
        GUI.SetNextControlName("MessageInput");
        
        // Handle Enter key for sending messages
        bool shouldSend = HandleInputKeyEvents();
        
        EditorGUILayout.BeginVertical();
        EditorGUILayout.LabelField("Message:");
        inputMessage = EditorGUILayout.TextArea(inputMessage, GUILayout.Height(60), GUILayout.ExpandWidth(true));
        EditorGUILayout.EndVertical();
        
        // Show appropriate button(s) based on streaming state
        bool sendButtonPressed = false;
        bool stopButtonPressed = false;
        
        if (isWaitingForAI || currentlyStreamingMessage != null)
        {
            // Show stop button when streaming/waiting
            stopButtonPressed = GUILayout.Button("⏹️ Stop", GUILayout.Width(60), GUILayout.Height(60));
        }
        else
        {
            // Show send button when not streaming
            GUI.enabled = !isWaitingForAI;
            sendButtonPressed = GUILayout.Button("Send", GUILayout.Width(60), GUILayout.Height(60));
            GUI.enabled = true;
        }
        
        if ((shouldSend || sendButtonPressed) && !string.IsNullOrEmpty(inputMessage.Trim()) && !isWaitingForAI)
        {
            SendMessage();
            RefocusInputField();
        }
        
        // Handle stop button press
        if (stopButtonPressed)
        {
            StopStreaming();
        }
        
        EditorGUILayout.EndHorizontal();
        
        DrawSuggestions();
        DrawHelpText();
        
        EditorGUILayout.EndVertical();
    }
    
    private bool HandleInputKeyEvents()
    {
        bool enterPressed = false;
        bool consumeEnterEvent = false;
        
        if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return)
        {
            string focusedControl = GUI.GetNameOfFocusedControl();
            if (focusedControl == "MessageInput" || string.IsNullOrEmpty(focusedControl))
            {
                if (!Event.current.shift && !Event.current.control && !Event.current.alt)
                {
                    enterPressed = true;
                    consumeEnterEvent = true;
                }
            }
        }
        
        if (consumeEnterEvent)
        {
            Event.current.Use();
        }
        
        return enterPressed;
    }
    
    private void DrawSuggestions()
    {
        if (suggestionSystem.CurrentSuggestions != null && suggestionSystem.CurrentSuggestions.Length > 0)
        {
            EditorGUILayout.Space();
            
            GUIStyle headerStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                fontStyle = FontStyle.Bold,
                normal = { textColor = EditorGUIUtility.isProSkin ? Color.gray : new Color(0.3f, 0.3f, 0.3f, 1f) }
            };
            EditorGUILayout.LabelField("💡 Quick suggestions:", headerStyle);
            
            EditorGUILayout.Space(2);
            suggestionSystem.DrawSuggestionButtons(position, isWaitingForAI, SendSuggestion);
        }
    }
    
    private void DrawSystemMessageLabel()
    {
        if (!string.IsNullOrEmpty(lastSystemMessage))
        {
            // Create a style for the system message
            GUIStyle systemMessageStyle = new GUIStyle(EditorStyles.helpBox)
            {
                fontSize = 11,
                fontStyle = FontStyle.Normal,
                wordWrap = true,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(8, 8, 4, 4)
            };
            
            // Set color based on message type/content
            if (lastSystemMessage.Contains("Error") || lastSystemMessage.Contains("❌"))
            {
                systemMessageStyle.normal.textColor = EditorGUIUtility.isProSkin ? new Color(1f, 0.6f, 0.6f) : new Color(0.8f, 0.2f, 0.2f);
            }
            else if (lastSystemMessage.Contains("✅") || lastSystemMessage.Contains("compiled successfully"))
            {
                systemMessageStyle.normal.textColor = EditorGUIUtility.isProSkin ? new Color(0.6f, 1f, 0.6f) : new Color(0.2f, 0.6f, 0.2f);
            }
            else
            {
                systemMessageStyle.normal.textColor = EditorGUIUtility.isProSkin ? Color.white : Color.black;
            }
            
            EditorGUILayout.LabelField(lastSystemMessage, systemMessageStyle, GUILayout.MaxHeight(40));
            EditorGUILayout.Space(2);
        }
    }
    
    private void DrawHelpText()
    {
        EditorGUILayout.Space(2);
        string helpText = aiEnabled ? 
            "Chat with Claude AI with real-time streaming! Ask it to create scripts, objects, or manipulate your Unity scene. Press Enter to send, Shift+Enter for new line." :
            "AI is disabled. Enable it to chat with Claude. Press Enter or click Send to send messages.";
        EditorGUIUtility.AddCursorRect(GUILayoutUtility.GetLastRect(), MouseCursor.Arrow);
        EditorGUILayout.HelpBox(helpText, UnityEditor.MessageType.Info);
    }
    
    private async void SendMessage()
    {
        if (string.IsNullOrEmpty(inputMessage.Trim())) return;
        
        string userMessage = inputMessage.Trim();
        inputMessage = "";
        
        // Clear keyboard focus to ensure input field updates immediately
        GUIUtility.keyboardControl = 0;
        // Force immediate GUI update to clear input field
        Repaint();
        
        AddMessage(ChatMessage.CreateUserMessage(currentUsername, userMessage));
        conversationHistory.Add(ClaudeMessage.CreateTextMessage("user", userMessage));
        
        // User message added to conversation history
        
        ScrollToBottom();
        
        // Handle commands
        if (userMessage.StartsWith("/"))
        {
            commandHandler.HandleCommand(userMessage);
            suggestionSystem.UpdateSuggestions(aiEnabled, messages, isWaitingForAI || currentlyStreamingMessage != null);
            return;
        }
        
        // Send to AI if enabled
        if (aiEnabled)
        {
            await ProcessAIResponse(userMessage);
        }
        else
        {
            // For non-AI messages, add directly without affecting isWaitingForAI
            var systemMessage = ChatMessage.CreateSystemMessage("AI is currently disabled.", MessageType.System);
            messages.Add(systemMessage);
            _ = StreamSystemMessageAsync(systemMessage); // Fire and forget
            ScrollToBottom();
            Repaint();
        }
        
        suggestionSystem.UpdateSuggestions(aiEnabled, messages, isWaitingForAI || currentlyStreamingMessage != null);
    }
    
    private async System.Threading.Tasks.Task ProcessAIResponse(string userMessage)
    {
        isWaitingForAI = true;
        
        await ProcessStreamingAIResponse(userMessage);
        
        suggestionSystem.UpdateSuggestions(aiEnabled, messages, isWaitingForAI || currentlyStreamingMessage != null);
        ScrollToBottom();
        Repaint();
    }
    
    private async System.Threading.Tasks.Task ProcessStreamingAIResponse(string userMessage)
    {
        try
        {
            SendDebugMessageInstance($"ProcessStreamingAIResponse started with userMessage: '{userMessage}'");
            SendDebugMessageInstance($"ConversationHistory count before API call: {conversationHistory?.Count ?? 0}");
            
            // Create streaming message
            var streamingMessage = ChatMessage.CreateStreamingMessage("Claude", MessageType.Normal);
            
            // Add the message to the UI immediately
            messages.Add(streamingMessage);
            currentlyStreamingMessage = streamingMessage;
            ScrollToBottom();
            Repaint();

            SendDebugMessageInstance("About to call ClaudeAIAgent.SendMessageStreamAsync");
            string aiResponse = await ClaudeAIAgent.SendMessageStreamAsync(
                userMessage, 
                conversationHistory, 
                (textDelta) => OnUnifiedStreamingTextDelta(streamingMessage, textDelta)
            );

            SendDebugMessageInstance($"ClaudeAIAgent.SendMessageStreamAsync returned: '{aiResponse}' (length: {aiResponse?.Length ?? 0})");

            // Complete the streaming
            streamingMessage.message = aiResponse;
            streamingMessage.CompleteStreaming();
            currentlyStreamingMessage = null;
            
            // Add to conversation history
            conversationHistory.Add(ClaudeMessage.CreateTextMessage("assistant", aiResponse));
            
            SendDebugMessageInstance($"Added Claude response to conversation history. New count: {conversationHistory?.Count ?? 0}");
            
            // Process any queued error batches after normal AI response
            errorHandler.ProcessQueuedErrors(suggestionSystem, messages);
        }
        catch (System.Exception ex)
        {
            // Clean up streaming state
            if (currentlyStreamingMessage != null)
            {
                currentlyStreamingMessage.CompleteStreaming();
                currentlyStreamingMessage = null;
            }
            
            Debug.LogError($"[ChatWindow] AI processing error: {ex.Message}");
            QueueMessage(ChatMessage.CreateSystemMessage($"AI Error: {ex.Message}", MessageType.Error));
        }
        finally
        {
            isWaitingForAI = false;
            
            // Reset compilation message reference if no compilation is happening
            if (!EditorApplication.isCompiling)
            {
                currentCompilationWaitMessage = null;
            }
            ScrollToBottom();
            Repaint();
        }
    }



    private void OnStreamingTextDelta(string textDelta)
    {
        if (currentlyStreamingMessage != null)
        {
            currentlyStreamingMessage.AppendText(textDelta);
            
            // Update UI on main thread
            EditorApplication.delayCall += () => {
                ScrollToBottom();
                Repaint();
            };
        }
    }
    
    private void OnUnifiedStreamingTextDelta(ChatMessage message, string textDelta)
    {
        if (message != null && message.isStreaming)
        {
            message.AppendText(textDelta);
            
            // Update UI on main thread
            EditorApplication.delayCall += () => {
                ScrollToBottom();
                Repaint();
            };
        }
    }
    
    // Unified method to queue messages for streaming
    public void QueueMessage(ChatMessage message, bool insertAboveStreaming = false, 
                           System.Action<string> onTextDelta = null, System.Action onComplete = null)
    {
        // If it's a system message, update the system message label instead of adding to chat
        if (message.type == MessageType.System || message.type == MessageType.Error)
        {
            UpdateSystemMessage(message.message);
            return;
        }
        
        var queueEntry = new MessageQueueEntry(message, insertAboveStreaming, onTextDelta, onComplete);
        messageQueue.Enqueue(queueEntry);
        
        // Start processing if not already running
        if (!isProcessingQueue)
        {
            ProcessNextMessage();
        }
    }
    
    // Legacy method for backward compatibility - now simplified
    private void AddMessage(ChatMessage message)
    {
        // For system messages, update the system message label instead of adding to chat
        if (message.type == MessageType.System || message.type == MessageType.Error)
        {
            UpdateSystemMessage(message.message);
            return;
        }
        
        // For normal messages, add directly
        messages.Add(message);
        
        ScrollToBottom();
        Repaint();
    }
    
    private void UpdateSystemMessage(string message)
    {
        lastSystemMessage = message;
        lastSystemMessageTime = EditorApplication.timeSinceStartup;
        
        // Force GUI update to show the new system message
        Repaint();
    }
    
    private async void ProcessNextMessage()
    {
        if (isProcessingQueue || messageQueue.Count == 0)
            return;
            
        isProcessingQueue = true;
        
        while (messageQueue.Count > 0)
        {
            var entry = messageQueue.Dequeue();
            var message = entry.message;
            
            // System messages should not reach here due to QueueMessage filtering, but handle just in case
            if (message.type == MessageType.System || message.type == MessageType.Error)
            {
                UpdateSystemMessage(message.message);
                entry.onComplete?.Invoke();
                continue;
            }
            
            // Handle insertion above streaming message
            if (entry.requiresInsertionAboveStreaming && currentlyStreamingMessage != null)
            {
                int streamingIndex = messages.IndexOf(currentlyStreamingMessage);
                if (streamingIndex >= 0)
                {
                    messages.Insert(streamingIndex, message);
                }
                else
                {
                    messages.Add(message);
                }
            }
            else
            {
                messages.Add(message);
            }
            
            // Handle streaming
            if (message.isStreaming)
            {
                // Only set as currently streaming if this is not an AI response being handled elsewhere
                if (entry.onTextDelta == null && currentlyStreamingMessage == null)
                {
                    currentlyStreamingMessage = message;
                }
                
                // For non-AI messages, simulate streaming by adding text character by character
                if (entry.onTextDelta == null)
                {
                    await StreamSystemMessage(message);
                    message.CompleteStreaming();
                    
                    // Only clear if we set it
                    if (currentlyStreamingMessage == message)
                    {
                        currentlyStreamingMessage = null;
                    }
                }
                
                // Call completion callback if provided
                entry.onComplete?.Invoke();
            }
            
            ScrollToBottom();
            Repaint();
        }
        
        isProcessingQueue = false;
    }
    
    private async System.Threading.Tasks.Task StreamSystemMessage(ChatMessage message)
    {
        var originalMessage = message.message;
        message.message = "";
        
        // Stream character by character for system messages - very fast for system messages
        for (int i = 0; i < originalMessage.Length; i++)
        {
            await System.Threading.Tasks.Task.Delay(2); // Much faster for system messages
            message.message += originalMessage[i];
            
            EditorApplication.delayCall += () => {
                ScrollToBottom();
                Repaint();
            };
        }
    }
    
    private async System.Threading.Tasks.Task StreamSystemMessageAsync(ChatMessage message)
    {
        var originalMessage = message.message;
        message.message = "";
        
        // Stream character by character for system messages - very fast for system messages
        for (int i = 0; i < originalMessage.Length; i++)
        {
            await System.Threading.Tasks.Task.Delay(2); // Much faster for system messages
            message.message += originalMessage[i];
            
            EditorApplication.delayCall += () => {
                ScrollToBottom();
                Repaint();
            };
        }
        
        message.CompleteStreaming();
    }
    
    private async System.Threading.Tasks.Task StreamMessageContent(ChatMessage message, System.Action<string> onTextDelta)
    {
        // This will be called by the AI streaming logic
        // The onTextDelta callback will update the message content
        // We just need to wait for the streaming to complete
        while (message.isStreaming)
        {
            await System.Threading.Tasks.Task.Delay(50);
        }
    }
    
    // Note: Message removal is no longer needed with unified streaming queue system
    // All messages go through the queue and are properly managed
    
    private void StopStreaming()
    {
        try
        {
            // Stop the AI streaming via ClaudeAIAgent
            ClaudeAIAgent.StopStreaming();
            
            // Clean up any current streaming message
            if (currentlyStreamingMessage != null)
            {
                currentlyStreamingMessage.CompleteStreaming();
                currentlyStreamingMessage = null;
            }
            
            // Reset waiting state
            isWaitingForAI = false;
            
            // Show stop message to user
            UpdateSystemMessage("⏹️ Streaming stopped by user");
            
            // Force UI update
            ScrollToBottom();
            Repaint();
            
            Debug.Log("[ChatWindow] Streaming stopped by user request");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[ChatWindow] Error stopping streaming: {ex.Message}");
            UpdateSystemMessage($"❌ Error stopping stream: {ex.Message}");
        }
    }
    
    private void SendSuggestion(string suggestion)
    {
        if (string.IsNullOrEmpty(suggestion))
            return;
            
        // Handle stop streaming suggestion
        if (suggestion == "⏹️ Stop streaming")
        {
            StopStreaming();
            return;
        }
        
        // Handle normal suggestions
        if (isWaitingForAI)
            return;
            
        inputMessage = suggestion;
        SendMessage();
        suggestionSystem.UpdateSuggestions(aiEnabled, messages, isWaitingForAI || currentlyStreamingMessage != null);
    }
    
    private void RefocusInputField()
    {
        EditorApplication.delayCall += () => {
            GUI.FocusControl("MessageInput");
            Repaint();
        };
    }
    
    private void ClearMessages()
    {
        messages.Clear();
        conversationHistory.Clear();
        messageQueue.Clear();
        
        // Reset all state variables to ensure UI returns to normal
        isWaitingForAI = false;
        currentlyStreamingMessage = null;
        currentCompilationWaitMessage = null;
        isProcessingQueue = false;
        
        // Clear any pending compilation notifications
        SessionState.SetBool(SHOULD_NOTIFY_CLAUDE_KEY, false);
        
        // Clear system message and show cleared message
        lastSystemMessage = "";
        lastSystemMessageTime = 0;
        UpdateSystemMessage("Chat cleared.");
        
        // Clear saved history too
        ClearSavedChatHistory();
        
        // Update suggestions after clearing
        suggestionSystem.UpdateSuggestions(aiEnabled, messages, isWaitingForAI || currentlyStreamingMessage != null);
    }
    
    private void ScrollToBottom()
    {
        scrollPosition.y = float.MaxValue;
    }
    
    private void CopyConversationToClipboard()
    {
        ChatClipboardManager.CopyConversationToClipboard(messages, consoleCapture.CapturedLogs, consoleCapture.IncludeLogs);
        
        string logInfo = consoleCapture.IncludeLogs ? $" + {consoleCapture.CapturedLogs.Count} console logs" : "";
        QueueMessage(ChatMessage.CreateSystemMessage($"Conversation copied to clipboard! ({messages.Count - 1} messages{logInfo})", MessageType.System));
    }
    
    // Event-based compilation tracking - these events persist across assembly reloads

    
    // Simplified compilation result processing (no more complex state tracking)
    private void ProcessCompilationResults()
    {
        Debug.Log($"[ChatWindow] ProcessCompilationResults called");
        
        // Send debug info to chat window
        SendDebugMessageInstance($"ProcessCompilationResults called - Messages:{messages.Count}");
        UpdateSystemMessage($"🔧 DEBUG: ProcessCompilationResults called - Messages:{messages.Count}, ConvHistory:{conversationHistory.Count}");
        
        // Clear compilation wait message reference
        currentCompilationWaitMessage = null;
        
        // Always ensure isWaitingForAI is false after compilation
        isWaitingForAI = false;
        
        // Check compilation results and notify Claude
        CheckCompilationResultsAndNotifyClaude();
    }
    
    private void CheckCompilationResultsAndNotifyClaude()
    {
        Debug.Log($"[ChatWindow] CheckCompilationResultsAndNotifyClaude called");
        
        try
        {
            // Check if compilation was successful (no recent errors)
            bool hasRecentErrors = consoleCapture?.HasRecentErrors() ?? false;
            bool success = !hasRecentErrors;
            
            SendDebugMessageInstance($"CheckCompilationResultsAndNotifyClaude - hasRecentErrors: {hasRecentErrors}, success: {success}");
            Debug.Log($"[ChatWindow] Compilation check - hasRecentErrors: {hasRecentErrors}, success: {success}");
        
        if (success)
        {
            // Use custom success message if provided, otherwise use default
            string successMessage;
            if (customSuccessMessageProvider != null)
            {
                successMessage = customSuccessMessageProvider(true);
                // Clear the callback after use
                customSuccessMessageProvider = null;
            }
            else
            {
                successMessage = "Compilation successful";
            }
            
            // Send compilation success as a USER message that will be sent to Claude
            var successUserMessage = ChatMessage.CreateUserMessage("User", successMessage);
            QueueMessage(successUserMessage, false);
            
            // Add to conversation history for Claude and save to SessionState
            conversationHistory.Add(ClaudeMessage.CreateTextMessage("user", successMessage));
            SaveConversationHistoryToSessionState();
            
            // Save the compilation message for post-compilation processing
            SessionState.SetString(COMPILATION_MESSAGE_KEY, successMessage);
            
            Debug.Log($"[ChatWindow] Added compilation success user message and saved to SessionState: {successMessage}");
        }
        else
        {
            // Handle compilation failure - get error details from console
            string errorDetails = "";
            if (consoleCapture != null)
            {
                var recentLogs = consoleCapture.CapturedLogs;
                var recentErrors = recentLogs.Where(log => 
                    log.type == LogType.Error || log.type == LogType.Exception)
                    .TakeLast(3)
                    .Select(log => log.logString);
                
                if (recentErrors.Any())
                {
                    errorDetails = string.Join("\n", recentErrors);
                }
            }
            
            string failureMessage;
            if (customSuccessMessageProvider != null)
            {
                failureMessage = customSuccessMessageProvider(false);
                // Clear the callback after use
                customSuccessMessageProvider = null;
            }
            else
            {
                failureMessage = string.IsNullOrEmpty(errorDetails) ? 
                    "Compilation error: Unknown compilation failure" :
                    $"Compilation error: {errorDetails}";
            }
            
            // Send compilation failure as a USER message that will be sent to Claude
            var failureUserMessage = ChatMessage.CreateUserMessage("User", failureMessage);
            QueueMessage(failureUserMessage, false);
            
            // Add to conversation history for Claude and save to SessionState
            conversationHistory.Add(ClaudeMessage.CreateTextMessage("user", failureMessage));
            SaveConversationHistoryToSessionState();
            
            // Save the compilation message for post-compilation processing
            SessionState.SetString(COMPILATION_MESSAGE_KEY, failureMessage);
            
            Debug.Log($"[ChatWindow] Added compilation failure user message and saved to SessionState: {failureMessage}");
        }
        
        // Trigger Claude to respond to the compilation result
        if (aiEnabled && !isWaitingForAI)
        {
            SendDebugMessageInstance("Triggering Claude response to compilation result");
            SendDebugMessageInstance($"ConversationHistory count: {conversationHistory?.Count ?? 0}");
            
            // Check if conversation history is empty after domain reload
            if (conversationHistory == null || conversationHistory.Count == 0)
            {
                SendDebugMessageInstance("ConversationHistory is empty after domain reload - restoring from SessionState");
                RestoreConversationHistoryFromSessionState();
                SendDebugMessageInstance($"After restore - ConversationHistory count: {conversationHistory?.Count ?? 0}");
            }
            
            // Debug: Show what's in conversation history before calling Claude
            if (conversationHistory != null && conversationHistory.Count > 0)
            {
                var lastMessage = conversationHistory.LastOrDefault();
                var preview = lastMessage?.content?[0]?.text?.Substring(0, Math.Min(50, lastMessage?.content?[0]?.text?.Length ?? 0)) ?? "[no text]";
                SendDebugMessageInstance($"Last conversation message: {lastMessage?.role} - {preview}...");
            }
            
            // Instead of empty string, pass a prompt asking Claude to acknowledge the compilation result
            string promptForClaude = "Please acknowledge the compilation result and provide any relevant feedback or next steps.";
            SendDebugMessageInstance($"Calling ProcessStreamingAIResponse with prompt: {promptForClaude}");
            
            // Call directly instead of using delayCall for reliability
            _ = ProcessStreamingAIResponse(promptForClaude);
        }
        else
        {
            SendDebugMessageInstance($"Not triggering Claude - aiEnabled: {aiEnabled}, isWaitingForAI: {isWaitingForAI}");
        }
        
        ScrollToBottom();
        Repaint();
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[ChatWindow] Error checking compilation results: {ex.Message}");
        }
    }
    
    private async void SendCompilationResultToClaude(string compilationMessage, bool success)
    {
        try
        {
            // Send debug info to chat window
            UpdateSystemMessage($"🔧 DEBUG: SendCompilationResultToClaude called - Success:{success}, Messages:{messages.Count}, ConvHistory:{conversationHistory.Count}");
            
            // Prevent duplicate calls within 2 seconds
            double currentTime = EditorApplication.timeSinceStartup;
            if (currentTime - lastCompilationResultTime < 2.0)
            {
                Debug.Log($"[ChatWindow] Skipping duplicate compilation result call (too recent: {currentTime - lastCompilationResultTime:F2}s ago)");
                UpdateSystemMessage($"🔧 DEBUG: Skipping duplicate call (too recent: {currentTime - lastCompilationResultTime:F2}s ago)");
                return;
            }
            lastCompilationResultTime = currentTime;
            
            Debug.Log($"[ChatWindow] SendCompilationResultToClaude called - Success: {success}, Message: {compilationMessage}");
            Debug.Log($"[ChatWindow] Current conversation history count: {conversationHistory.Count}");
            Debug.Log($"[ChatWindow] Current messages count: {messages.Count}");
            Debug.Log($"[ChatWindow] AI enabled: {aiEnabled}, waiting for AI: {isWaitingForAI}");
            
            // Create a user message for Claude with compilation context and request for response
            string contextualPrompt = success ? 
                "The script/code you just created or modified has compiled successfully." :
                "The script/code you just created or modified failed to compile.";
                
            string claudeMessage = $"[SYSTEM NOTIFICATION - COMPILATION RESULT]\n{contextualPrompt}\n\nResult: {compilationMessage}\n\nPlease acknowledge this compilation result and provide any relevant feedback, suggestions, or next steps based on this outcome.";
            
            Debug.Log($"[ChatWindow] Claude message being sent: {claudeMessage.Substring(0, Math.Min(100, claudeMessage.Length))}...");
            
            // Add to UI messages as a user message
            var userMessage = ChatMessage.CreateUserMessage("User", claudeMessage);
            messages.Add(userMessage);
            
            // Add to conversation history
            conversationHistory.Add(ClaudeMessage.CreateTextMessage("user", claudeMessage));
            
            Debug.Log($"[ChatWindow] After adding compilation result - conversation history count: {conversationHistory.Count}");
            Debug.Log($"[ChatWindow] After adding compilation result - messages count: {messages.Count}");
            
            // Debug: Show all conversation history entries
            Debug.Log($"[ChatWindow] Full conversation history after adding compilation result:");
            for (int i = 0; i < conversationHistory.Count; i++)
            {
                var entry = conversationHistory[i];
                var preview = entry.content?[0]?.text?.Substring(0, Math.Min(30, entry.content?[0]?.text?.Length ?? 0)) ?? "[no text]";
                Debug.Log($"[ChatWindow]   {i}: {entry.role} - {preview}...");
            }
            
            Debug.Log($"[ChatWindow] Sent compilation result to Claude: {(success ? "SUCCESS" : "FAILURE")}");
            
            // Trigger Claude to respond to the compilation result
            if (aiEnabled && !isWaitingForAI)
            {
                Debug.Log($"[ChatWindow] Scheduling Claude response to compilation result");
                UpdateSystemMessage($"🔧 DEBUG: Scheduling Claude response to compilation result");
                // Add a small delay to ensure UI updates are complete
                EditorApplication.delayCall += () =>
                {
                    Debug.Log($"[ChatWindow] Executing delayed Claude response to compilation result");
                    UpdateSystemMessage($"🔧 DEBUG: Executing delayed Claude response to compilation result");
                    ScrollToBottom();
                    Repaint();
                    _ = ProcessCompilationResultResponse(); // Fire and forget
                };
            }
            else
            {
                Debug.Log($"[ChatWindow] Skipping Claude response - AI disabled: {!aiEnabled}, waiting for AI: {isWaitingForAI}");
                UpdateSystemMessage($"🔧 DEBUG: Skipping Claude response - AI disabled: {!aiEnabled}, waiting for AI: {isWaitingForAI}");
            }
            
            ScrollToBottom();
            Repaint();
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[ChatWindow] Failed to send compilation result to Claude: {ex.Message}");
        }
    }

    private async System.Threading.Tasks.Task ProcessCompilationResultResponse()
    {
        try
        {
            Debug.Log($"[ChatWindow] ProcessCompilationResultResponse started");
            Debug.Log($"[ChatWindow] Conversation history count before response: {conversationHistory.Count}");
            Debug.Log($"[ChatWindow] Last 3 conversation entries:");
            for (int i = Math.Max(0, conversationHistory.Count - 3); i < conversationHistory.Count; i++)
            {
                var entry = conversationHistory[i];
                var preview = entry.content?[0]?.text?.Substring(0, Math.Min(50, entry.content?[0]?.text?.Length ?? 0)) ?? "[no text]";
                Debug.Log($"[ChatWindow]   {i}: {entry.role} - {preview}...");
            }
            
            isWaitingForAI = true;
            
            // Create streaming message
            var streamingMessage = ChatMessage.CreateStreamingMessage("Claude", MessageType.Normal);
            
            // Add the message to the UI immediately
            messages.Add(streamingMessage);
            currentlyStreamingMessage = streamingMessage;
            ScrollToBottom();
            Repaint();

            Debug.Log($"[ChatWindow] Sending continuation prompt to Claude");
            
            // Send a direct request to Claude using the existing conversation history
            // We'll send a simple continuation prompt to get Claude to respond to the compilation result
            string aiResponse = await ClaudeAIAgent.SendMessageStreamAsync(
                "Please respond to the above compilation result.", 
                conversationHistory, 
                (textDelta) => OnUnifiedStreamingTextDelta(streamingMessage, textDelta)
            );

            Debug.Log($"[ChatWindow] Received Claude response: {aiResponse?.Substring(0, Math.Min(100, aiResponse?.Length ?? 0))}...");

            // Complete the streaming
            streamingMessage.message = aiResponse;
            streamingMessage.CompleteStreaming();
            currentlyStreamingMessage = null;
            
            // Add to conversation history
            conversationHistory.Add(ClaudeMessage.CreateTextMessage("assistant", aiResponse));
            
            Debug.Log($"[ChatWindow] Claude responded to compilation result - final conversation history count: {conversationHistory.Count}");
        }
        catch (System.Exception ex)
        {
            // Clean up streaming state
            if (currentlyStreamingMessage != null)
            {
                currentlyStreamingMessage.CompleteStreaming();
                currentlyStreamingMessage = null;
            }
            
            Debug.LogError($"[ChatWindow] AI processing error for compilation result: {ex.Message}");
            QueueMessage(ChatMessage.CreateSystemMessage($"AI Error: {ex.Message}", MessageType.Error));
        }
        finally
        {
            isWaitingForAI = false;
            ScrollToBottom();
            Repaint();
        }
    }
    
    // Helper method to extract content preview from ClaudeContentBlock list
    private string GetContentPreview(List<ClaudeContentBlock> contentBlocks)
    {
        if (contentBlocks == null || contentBlocks.Count == 0)
            return "[empty]";
            
        var textContent = new System.Text.StringBuilder();
        int totalLength = 0;
        
        foreach (var block in contentBlocks)
        {
            string blockText = "";
            
            if (block.type == "text" && !string.IsNullOrEmpty(block.text))
            {
                blockText = block.text;
            }
            else if (block.type == "tool_use" && !string.IsNullOrEmpty(block.name))
            {
                blockText = $"[tool: {block.name}]";
            }
            else if (block.type == "tool_result")
            {
                blockText = $"[tool_result: {block.tool_use_id}]";
            }
            else
            {
                blockText = $"[{block.type}]";
            }
            
            if (totalLength + blockText.Length > 100)
            {
                int remainingLength = 100 - totalLength;
                if (remainingLength > 0)
                {
                    textContent.Append(blockText.Substring(0, remainingLength));
                }
                textContent.Append("...");
                break;
            }
            
            textContent.Append(blockText);
            totalLength += blockText.Length;
            
            if (totalLength >= 100)
                break;
        }
        
        return textContent.ToString();
    }
    
    // Event handlers
    private void OnErrorBatchReceived(List<ErrorBatch> errorBatch)
    {
        errorHandler.OnErrorBatchReceived(errorBatch, aiEnabled && autoFixEnabled, consoleCapture, suggestionSystem, messages);
    }
    
    private void OnErrorFixingCompleted(bool success)
    {
        // Process any queued errors after error fixing is complete
        errorHandler.ProcessQueuedErrors(suggestionSystem, messages);
    }
    

    
    // Chat History Persistence Methods
    private void SaveChatHistory()
    {
        try
        {
            if (messages.Count > 0)
            {
                string messagesJson = JsonUtility.ToJson(new SerializableMessageList(messages));
                SessionState.SetString("ChatWindow_Messages", messagesJson);
                Debug.Log($"[ChatWindow] Saved {messages.Count} messages to SessionState");
            }
            
            if (conversationHistory.Count > 0)
            {
                string historyJson = JsonConvert.SerializeObject(conversationHistory);
                SessionState.SetString("ChatWindow_ConversationHistory", historyJson);
                Debug.Log($"[ChatWindow] Saved {conversationHistory.Count} conversation entries to SessionState");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[ChatWindow] Failed to save chat history: {ex.Message}");
        }
    }
    
    private void LoadChatHistory()
    {
        try
        {
            // Load UI messages
            string messagesJson = SessionState.GetString("ChatWindow_Messages", "");
            if (!string.IsNullOrEmpty(messagesJson))
            {
                var messageList = JsonUtility.FromJson<SerializableMessageList>(messagesJson);
                if (messageList?.messages != null)
                {
                    messages.AddRange(messageList.messages);
                    Debug.Log($"[ChatWindow] Restored {messages.Count} messages from SessionState");
                }
            }
            
            // Load Claude conversation history
            string historyJson = SessionState.GetString("ChatWindow_ConversationHistory", "");
            if (!string.IsNullOrEmpty(historyJson))
            {
                var history = JsonConvert.DeserializeObject<List<ClaudeMessage>>(historyJson);
                if (history != null)
                {
                    conversationHistory.AddRange(history);
                    Debug.Log($"[ChatWindow] Restored {conversationHistory.Count} conversation entries from SessionState");
                }
            }
            
            // Show restoration message if we loaded anything
            if (messages.Count > 0 || conversationHistory.Count > 0)
            {
                UpdateSystemMessage($"Chat history restored! ({messages.Count} messages, {conversationHistory.Count} conversation entries)");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[ChatWindow] Failed to load chat history: {ex.Message}");
            // Clear potentially corrupted data
            SessionState.EraseString("ChatWindow_Messages");
            SessionState.EraseString("ChatWindow_ConversationHistory");
        }
    }
    
    private void ClearSavedChatHistory()
    {
        SessionState.EraseString("ChatWindow_Messages");
        SessionState.EraseString("ChatWindow_ConversationHistory");
        Debug.Log("[ChatWindow] Cleared saved chat history from SessionState");
    }
    
    // Save conversation history to SessionState (survives domain reload)
    private void SaveConversationHistoryToSessionState()
    {
        try
        {
            string json = JsonConvert.SerializeObject(conversationHistory);
            SessionState.SetString(CONVERSATION_HISTORY_KEY, json);
            Debug.Log($"[ChatWindow] Saved conversation history to SessionState ({conversationHistory.Count} messages)");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ChatWindow] Failed to save conversation history: {ex.Message}");
        }
    }
    
    // Restore conversation history from SessionState (after domain reload)
    private void RestoreConversationHistoryFromSessionState()
    {
        try
        {
            string json = SessionState.GetString(CONVERSATION_HISTORY_KEY, "");
            if (!string.IsNullOrEmpty(json))
            {
                var restoredHistory = JsonConvert.DeserializeObject<List<ClaudeMessage>>(json);
                if (restoredHistory != null)
                {
                    conversationHistory = restoredHistory;
                    Debug.Log($"[ChatWindow] Restored conversation history from SessionState ({conversationHistory.Count} messages)");
                    
                    // Clear the saved history after restoring
                    SessionState.EraseString(CONVERSATION_HISTORY_KEY);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ChatWindow] Failed to restore conversation history: {ex.Message}");
        }
    }
}

// Helper class for serializing messages list
[System.Serializable]
public class SerializableMessageList
{
    public List<ChatMessage> messages;
    
    public SerializableMessageList(List<ChatMessage> messages)
    {
        this.messages = messages;
    }
} 