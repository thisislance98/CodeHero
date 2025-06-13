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
    private Vector2 scrollPosition;
    private bool aiEnabled = true;
    private bool isWaitingForAI = false;
    
    // System message display
    private string lastSystemMessage = "";
    private double lastSystemMessageTime = 0;
    
    // Unified message streaming system
    private Queue<MessageQueueEntry> messageQueue = new Queue<MessageQueueEntry>();
    private ChatMessage currentlyStreamingMessage = null;
    private bool isProcessingQueue = false;
    
    // Unified compilation tracking system
    private bool isWaitingForSuccessfulCompilation = false;
    private ChatMessage currentCompilationWaitMessage = null;
    private Func<bool, string> customSuccessMessageProvider = null;
    
    // Component managers
    private ChatMessageRenderer messageRenderer;
    private ChatConsoleCapture consoleCapture;
    private ChatCommandHandler commandHandler;
    private ChatSuggestionSystem suggestionSystem;
    private ChatWindowErrorHandler errorHandler;
    
    // Streaming settings
    private bool useStreaming = true;
    
    // Compilation result tracking
    private double lastCompilationResultTime = 0;
    private bool isProcessingCompilationResult = false;
    
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
        LoadChatHistory(); // Restore chat history first
        SetupWelcomeMessages();
        SetupEventHandlers();
        
        consoleCapture.StartCapturing();
        
        // Start CLI monitoring
        ChatWindowCLI.StartMonitoring();
    }
    
    private void OnDisable()
    {
        // Save chat history before window closes
        SaveChatHistory();
        
        // Stop CLI monitoring
        ChatWindowCLI.StopMonitoring();
        
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
        suggestionSystem.UpdateSuggestions(aiEnabled, messages);
    }
    
    private void SetupWelcomeMessages()
    {
        if (messages.Count == 0 && string.IsNullOrEmpty(lastSystemMessage))
        {
            string welcomeMessage = "Welcome to Unity Chat Window with Claude AI! " +
                                  "AI is enabled by default - ask Claude to create scripts, GameObjects, or help with Unity tasks. " +
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
        
        // Compilation events
        EditorApplication.update += OnEditorUpdate;
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
        
        // Compilation events
        EditorApplication.update -= OnEditorUpdate;
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
    
    private void OnGUI()
    {
        EditorGUILayout.BeginVertical(GUILayout.ExpandHeight(true));
        
        DrawHeader();
        DrawMessagesArea();
        DrawInputArea();
        
        EditorGUILayout.EndVertical();
    }
    
    private void DrawHeader()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        
        GUILayout.Label("Chat Window", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        
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
    
    private void DrawMessagesArea()
    {
        // Calculate available height for messages area
        // Reserve space for input area (approximately 120 pixels for input + system message)
        float reservedHeight = 120f;
        float availableHeight = position.height - reservedHeight;
        
        EditorGUILayout.BeginVertical(GUILayout.ExpandHeight(true), GUILayout.MinHeight(200));
        messageRenderer.DrawMessagesArea(messages, ref scrollPosition);
        EditorGUILayout.EndVertical();
    }
    
    private void DrawInputArea()
    {
        EditorGUILayout.Space();
        
        // Draw system message label if we have one
        DrawSystemMessageLabel();
        
        EditorGUILayout.BeginHorizontal();
        
        GUI.SetNextControlName("MessageInput");
        
        // Handle Enter key for sending messages
        bool shouldSend = HandleInputKeyEvents();
        
        EditorGUILayout.BeginVertical();
        EditorGUILayout.LabelField("Message:");
        inputMessage = EditorGUILayout.TextArea(inputMessage, GUILayout.Height(60));
        EditorGUILayout.EndVertical();
        
        GUI.enabled = !isWaitingForAI;
        bool sendButtonPressed = GUILayout.Button("Send", GUILayout.Width(60));
        GUI.enabled = true;
        
        if ((shouldSend || sendButtonPressed) && !string.IsNullOrEmpty(inputMessage.Trim()) && !isWaitingForAI)
        {
            SendMessage();
            RefocusInputField();
        }
        
        EditorGUILayout.EndHorizontal();
        
        DrawSuggestions();
        DrawHelpText();
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
        EditorGUILayout.Space();
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
        
        AddMessage(new ChatMessage(currentUsername, userMessage));
        conversationHistory.Add(ClaudeMessage.CreateTextMessage("user", userMessage));
        
        // User message added to conversation history
        
        ScrollToBottom();
        
        // Handle commands
        if (userMessage.StartsWith("/"))
        {
            commandHandler.HandleCommand(userMessage);
            suggestionSystem.UpdateSuggestions(aiEnabled, messages);
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
            var systemMessage = new ChatMessage("System", "AI is currently disabled.", MessageType.System, true);
            messages.Add(systemMessage);
            _ = StreamSystemMessageAsync(systemMessage); // Fire and forget
            ScrollToBottom();
            Repaint();
        }
        
        suggestionSystem.UpdateSuggestions(aiEnabled, messages);
    }
    
    private async System.Threading.Tasks.Task ProcessAIResponse(string userMessage)
    {
        isWaitingForAI = true;
        
        await ProcessStreamingAIResponse(userMessage);
        
        suggestionSystem.UpdateSuggestions(aiEnabled, messages);
        ScrollToBottom();
        Repaint();
    }
    
    private async System.Threading.Tasks.Task ProcessStreamingAIResponse(string userMessage)
    {
        try
        {
            
            // Create streaming message
            var streamingMessage = new ChatMessage("Claude", "", MessageType.Normal, true);
            
            // Add the message to the UI immediately
            messages.Add(streamingMessage);
            currentlyStreamingMessage = streamingMessage;
            ScrollToBottom();
            Repaint();

            string aiResponse = await ClaudeAIAgent.SendMessageStreamAsync(
                userMessage, 
                conversationHistory, 
                (textDelta) => OnUnifiedStreamingTextDelta(streamingMessage, textDelta)
            );

            // Complete the streaming
            streamingMessage.message = aiResponse;
            streamingMessage.CompleteStreaming();
            currentlyStreamingMessage = null;
            
            // Add to conversation history
            conversationHistory.Add(ClaudeMessage.CreateTextMessage("assistant", aiResponse));
            
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
            QueueMessage(new ChatMessage("System", $"AI Error: {ex.Message}", MessageType.Error, true));
        }
        finally
        {
            isWaitingForAI = false;
            
            // Reset successful compilation state if no compilation is happening
            if (!EditorApplication.isCompiling && !isWaitingForSuccessfulCompilation)
            {
                isWaitingForSuccessfulCompilation = false;
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
    
    private void SendSuggestion(string suggestion)
    {
        if (string.IsNullOrEmpty(suggestion) || isWaitingForAI)
            return;
            
        inputMessage = suggestion;
        SendMessage();
        suggestionSystem.UpdateSuggestions(aiEnabled, messages);
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
        isWaitingForSuccessfulCompilation = false;
        currentlyStreamingMessage = null;
        currentCompilationWaitMessage = null;
        isProcessingQueue = false;
        
        // Clear system message and show cleared message
        lastSystemMessage = "";
        lastSystemMessageTime = 0;
        UpdateSystemMessage("Chat cleared.");
        
        // Clear saved history too
        ClearSavedChatHistory();
    }
    
    private void ScrollToBottom()
    {
        scrollPosition.y = float.MaxValue;
    }
    
    private void CopyConversationToClipboard()
    {
        ChatClipboardManager.CopyConversationToClipboard(messages, consoleCapture.CapturedLogs, consoleCapture.IncludeLogs);
        
        string logInfo = consoleCapture.IncludeLogs ? $" + {consoleCapture.CapturedLogs.Count} console logs" : "";
        QueueMessage(new ChatMessage("System", $"Conversation copied to clipboard! ({messages.Count - 1} messages{logInfo})", MessageType.System, true));
    }
    
    private void OnEditorUpdate()
    {        
        // Track compilation state changes for error fixing cycles only
        // Script creation tools now provide immediate feedback, so we don't need compilation tracking for them
        if (isWaitingForSuccessfulCompilation)
        {
            if (!EditorApplication.isCompiling)
            {
                // Compilation just finished - check if it was successful
                OnSuccessfulCompilationFinished();
                isWaitingForSuccessfulCompilation = false;
            }
        }
        else if (EditorApplication.isCompiling && (customSuccessMessageProvider != null || errorHandler.IsInErrorFixingCycle))
        {
            // Compilation started during error fixing (but not during normal AI tool use)
            OnSuccessfulCompilationStarted();
            isWaitingForSuccessfulCompilation = true;
        }
    }
    
    private void OnSuccessfulCompilationStarted()
    {
        // Add compilation message
        currentCompilationWaitMessage = new ChatMessage("System", "⚙️ Compiling scripts...", MessageType.System, true);
        QueueMessage(currentCompilationWaitMessage, true); // Insert above streaming messages
        ScrollToBottom();
        Repaint();
    }
    
    private void OnSuccessfulCompilationFinished()
    {
        // Note: Compilation message will complete naturally through streaming queue
        currentCompilationWaitMessage = null;
        
        // Always ensure isWaitingForAI is false after compilation
        isWaitingForAI = false;
        
        // Wait a moment then check for errors
        EditorApplication.delayCall += () =>
        {
            EditorApplication.delayCall += () => CheckSuccessfulCompilation();
        };
    }
    
    private void CheckSuccessfulCompilation()
    {
        Debug.Log($"[ChatWindow] CheckSuccessfulCompilation called - isProcessingCompilationResult: {isProcessingCompilationResult}");
        
        // Prevent duplicate processing
        if (isProcessingCompilationResult)
        {
            Debug.Log($"[ChatWindow] Already processing compilation result, skipping duplicate call");
            return;
        }
        
        isProcessingCompilationResult = true;
        
        try
        {
            // Check if compilation was successful (no recent errors)
            bool hasRecentErrors = consoleCapture?.HasRecentErrors() ?? false;
            bool success = !hasRecentErrors;
            
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
                successMessage = "✅ Scripts compiled successfully!";
            }
            
            // Show system message to user
            QueueMessage(new ChatMessage("System", successMessage, MessageType.System, useStreaming), true);
            
            // Send compilation result to Claude as user message for context
            SendCompilationResultToClaude(successMessage, true);
        }
        else
        {
            // Handle compilation failure
            string failureMessage = "";
            if (customSuccessMessageProvider != null)
            {
                failureMessage = customSuccessMessageProvider(false);
                QueueMessage(new ChatMessage("System", failureMessage, MessageType.System, useStreaming), true);
                // Clear the callback after use
                customSuccessMessageProvider = null;
                
                // Send compilation failure result to Claude as user message for context
                SendCompilationResultToClaude(failureMessage, false);
            }
            else
            {
                // Default failure message when no custom provider
                failureMessage = "❌ Compilation failed. Please check the console for errors.";
                QueueMessage(new ChatMessage("System", failureMessage, MessageType.System, useStreaming), true);
                
                // Send compilation failure result to Claude as user message for context
                SendCompilationResultToClaude(failureMessage, false);
            }
        }
        
        ScrollToBottom();
        Repaint();
        }
        finally
        {
            // Reset the flag so future compilation results can be processed
            isProcessingCompilationResult = false;
            Debug.Log($"[ChatWindow] CheckSuccessfulCompilation completed, reset isProcessingCompilationResult flag");
        }
    }
    
    private async void SendCompilationResultToClaude(string compilationMessage, bool success)
    {
        try
        {
            // Prevent duplicate calls within 2 seconds
            double currentTime = EditorApplication.timeSinceStartup;
            if (currentTime - lastCompilationResultTime < 2.0)
            {
                Debug.Log($"[ChatWindow] Skipping duplicate compilation result call (too recent: {currentTime - lastCompilationResultTime:F2}s ago)");
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
            var userMessage = new ChatMessage("User", claudeMessage, MessageType.Normal);
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
                // Add a small delay to ensure UI updates are complete
                EditorApplication.delayCall += () =>
                {
                    Debug.Log($"[ChatWindow] Executing delayed Claude response to compilation result");
                    ScrollToBottom();
                    Repaint();
                    _ = ProcessCompilationResultResponse(); // Fire and forget
                };
            }
            else
            {
                Debug.Log($"[ChatWindow] Skipping Claude response - AI disabled: {!aiEnabled}, waiting for AI: {isWaitingForAI}");
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
            var streamingMessage = new ChatMessage("Claude", "", MessageType.Normal, true);
            
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
            QueueMessage(new ChatMessage("System", $"AI Error: {ex.Message}", MessageType.Error, true));
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
        errorHandler.OnErrorBatchReceived(errorBatch, aiEnabled, consoleCapture, suggestionSystem, messages);
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