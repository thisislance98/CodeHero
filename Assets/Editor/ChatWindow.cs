using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;

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
    }
    
    private void OnDisable()
    {
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
        if (messages.Count == 0)
        {
            string welcomeMessage = "Welcome to Unity Chat Window with Claude AI! " +
                                  "AI is enabled by default - ask Claude to create scripts, GameObjects, or help with Unity tasks. " +
                                  "Example: 'Create a player movement script' or 'Create a red cube at position 0,5,0'. " +
                                  "Type /help for available commands. " +
                                  "Voice input available! Click the microphone button to start/stop voice recognition.";
            
            // Use streaming for welcome message too
            QueueMessage(new ChatMessage("System", welcomeMessage, MessageType.System, true));
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
        
        GUILayout.FlexibleSpace();
        
        DrawInputArea();
        
        EditorGUILayout.EndVertical();
    }
    
    private void DrawHeader()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        
        GUILayout.Label("Chat Window", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        
        // Add streaming toggle
        bool newUseStreaming = GUILayout.Toggle(useStreaming, "Streaming", EditorStyles.toolbarButton, GUILayout.Width(70));
        if (newUseStreaming != useStreaming)
        {
            useStreaming = newUseStreaming;
            QueueMessage(new ChatMessage("System", $"All message streaming {(useStreaming ? "enabled" : "disabled")}", MessageType.System, useStreaming));
        }
        
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
        messageRenderer.DrawMessagesArea(messages, ref scrollPosition);
    }
    
    private void DrawInputArea()
    {
        EditorGUILayout.Space();
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
    
    private void DrawHelpText()
    {
        EditorGUILayout.Space();
        string streamingStatus = useStreaming ? "with streaming" : "without streaming";
        string helpText = aiEnabled ? 
            $"Chat with Claude AI {streamingStatus}! Ask it to create scripts, objects, or manipulate your Unity scene. Press Enter to send, Shift+Enter for new line." :
            "AI is disabled. Enable it to chat with Claude. Press Enter or click Send to send messages.";
        EditorGUIUtility.AddCursorRect(GUILayoutUtility.GetLastRect(), MouseCursor.Arrow);
        EditorGUILayout.HelpBox(helpText, UnityEditor.MessageType.Info);
    }
    
    private async void SendMessage()
    {
        if (string.IsNullOrEmpty(inputMessage.Trim())) return;
        
        string userMessage = inputMessage.Trim();
        inputMessage = "";
        
        AddMessage(new ChatMessage(currentUsername, userMessage));
        conversationHistory.Add(ClaudeMessage.CreateTextMessage("user", userMessage));
        
        ScrollToBottom();
        Repaint();
        
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
            var systemMessage = new ChatMessage("System", "AI is currently disabled.", MessageType.System, useStreaming);
            if (useStreaming)
            {
                messages.Add(systemMessage);
                _ = StreamSystemMessageAsync(systemMessage); // Fire and forget
            }
            else
            {
                messages.Add(systemMessage);
            }
            ScrollToBottom();
            Repaint();
        }
        
        suggestionSystem.UpdateSuggestions(aiEnabled, messages);
    }
    
    private async System.Threading.Tasks.Task ProcessAIResponse(string userMessage)
    {
        isWaitingForAI = true;
        
        if (useStreaming)
        {
            await ProcessStreamingAIResponse(userMessage);
        }
        else
        {
            await ProcessNonStreamingAIResponse(userMessage);
        }
        
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
                false, // isErrorFix
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

    private async System.Threading.Tasks.Task ProcessNonStreamingAIResponse(string userMessage)
    {
        ChatMessage thinkingMessage = null;
        try
        {
            // Add thinking message directly
            thinkingMessage = new ChatMessage("Claude", "Thinking...", MessageType.System, useStreaming);
            if (useStreaming)
            {
                messages.Add(thinkingMessage);
                await StreamSystemMessage(thinkingMessage);
                thinkingMessage.CompleteStreaming();
            }
            else
            {
                messages.Add(thinkingMessage);
            }
            ScrollToBottom();
            Repaint();

            string aiResponse = await ClaudeAIAgent.SendMessageAsync(userMessage, conversationHistory);
            
            // Remove thinking message and add AI response
            if (thinkingMessage != null)
            {
                messages.Remove(thinkingMessage);
            }
            
            var responseMessage = new ChatMessage("Claude", aiResponse, MessageType.Normal, useStreaming);
            messages.Add(responseMessage);
            
            if (useStreaming)
            {
                await StreamSystemMessage(responseMessage);
                responseMessage.CompleteStreaming();
            }
            
            // Add to conversation history
            conversationHistory.Add(ClaudeMessage.CreateTextMessage("assistant", aiResponse));
            
            // Process any queued error batches after normal AI response
            errorHandler.ProcessQueuedErrors(suggestionSystem, messages);
        }
        catch (System.Exception ex)
        {
            // Remove thinking message on error
            if (thinkingMessage != null)
            {
                messages.Remove(thinkingMessage);
            }
            
            var errorMessage = new ChatMessage("System", $"AI Error: {ex.Message}", MessageType.Error, useStreaming);
            messages.Add(errorMessage);
            
            if (useStreaming)
            {
                await StreamSystemMessage(errorMessage);
                errorMessage.CompleteStreaming();
            }
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
        // For system messages, handle them directly without affecting AI state
        if (message.type == MessageType.System || message.type == MessageType.Error)
        {
            // Insert above streaming message if one exists
            if (currentlyStreamingMessage != null)
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
            
            // Stream if enabled
            if (useStreaming && message.isStreaming)
            {
                _ = StreamSystemMessageAsync(message); // Fire and forget
            }
        }
        else
        {
            // For normal messages, add directly
            messages.Add(message);
        }
        
        ScrollToBottom();
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
                
                // For system/non-AI messages, simulate streaming by adding text character by character
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
        
        // Stream character by character for system messages
        for (int i = 0; i < originalMessage.Length; i++)
        {
            await System.Threading.Tasks.Task.Delay(20); // Adjust speed as needed
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
        
        // Stream character by character for system messages
        for (int i = 0; i < originalMessage.Length; i++)
        {
            await System.Threading.Tasks.Task.Delay(20); // Adjust speed as needed
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
        
        QueueMessage(new ChatMessage("System", "Chat cleared.", MessageType.System, useStreaming));
    }
    
    private void ScrollToBottom()
    {
        scrollPosition.y = float.MaxValue;
    }
    
    private void CopyConversationToClipboard()
    {
        ChatClipboardManager.CopyConversationToClipboard(messages, consoleCapture.CapturedLogs, consoleCapture.IncludeLogs);
        
        string logInfo = consoleCapture.IncludeLogs ? $" + {consoleCapture.CapturedLogs.Count} console logs" : "";
        QueueMessage(new ChatMessage("System", $"Conversation copied to clipboard! ({messages.Count - 1} messages{logInfo})", MessageType.System, useStreaming));
    }
    
    private void OnEditorUpdate()
    {        
        // Track compilation state changes for successful script creation
        if (isWaitingForSuccessfulCompilation)
        {
            if (!EditorApplication.isCompiling)
            {
                // Compilation just finished - check if it was successful
                OnSuccessfulCompilationFinished();
                isWaitingForSuccessfulCompilation = false;
            }
        }
        else if (EditorApplication.isCompiling && (isWaitingForAI || customSuccessMessageProvider != null || errorHandler.IsInErrorFixingCycle))
        {
            // Compilation started during AI interaction or error fixing
            OnSuccessfulCompilationStarted();
            isWaitingForSuccessfulCompilation = true;
        }
    }
    
    private void OnSuccessfulCompilationStarted()
    {
        Debug.Log("[ChatWindow] Successful compilation started");
        
        // Add compilation message
        currentCompilationWaitMessage = new ChatMessage("System", "⚙️ Compiling scripts...", MessageType.System, useStreaming);
        QueueMessage(currentCompilationWaitMessage, true); // Insert above streaming messages
        ScrollToBottom();
        Repaint();
    }
    
    private void OnSuccessfulCompilationFinished()
    {
        Debug.Log("[ChatWindow] Successful compilation finished");
        
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
        // Check if compilation was successful (no recent errors)
        bool hasRecentErrors = consoleCapture?.HasRecentErrors() ?? false;
        bool success = !hasRecentErrors;
        
        Debug.Log($"[ChatWindow] Checking successful compilation: hasRecentErrors = {hasRecentErrors}");
        
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
            
            QueueMessage(new ChatMessage("System", successMessage, MessageType.System, useStreaming), true);
        }
        else
        {
            // Handle compilation failure
            if (customSuccessMessageProvider != null)
            {
                string failureMessage = customSuccessMessageProvider(false);
                QueueMessage(new ChatMessage("System", failureMessage, MessageType.System, useStreaming), true);
                // Clear the callback after use
                customSuccessMessageProvider = null;
            }
            else
            {
                // There were errors, the normal error fixing system will handle this
                Debug.Log("[ChatWindow] Compilation had errors - normal error system will handle");
            }
        }
        
        ScrollToBottom();
        Repaint();
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
} 