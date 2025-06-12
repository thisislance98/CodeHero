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
    
    // Message tracking for robust removal
    private ChatMessage currentThinkingMessage = null;
    
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
    
    [MenuItem("Tools/Chat Window")]
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
            AddMessage,
            RemoveMessageByReference,
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
            
            AddMessage(new ChatMessage("System", welcomeMessage, MessageType.System));
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
        string helpText = aiEnabled ? 
            "Chat with Claude AI! Ask it to create scripts, objects, or manipulate your Unity scene. Press Enter to send, Shift+Enter for new line." :
            "AI is disabled. Enable it to chat with Claude. Press Enter or click Send to send messages.";
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
            AddMessage(new ChatMessage("System", "AI is currently disabled.", MessageType.System));
        }
        
        suggestionSystem.UpdateSuggestions(aiEnabled, messages);
    }
    
    private async System.Threading.Tasks.Task ProcessAIResponse(string userMessage)
    {
        isWaitingForAI = true;
        currentThinkingMessage = new ChatMessage("Claude", "Thinking...", MessageType.System);
        AddMessage(currentThinkingMessage);
        ScrollToBottom();
        Repaint();
        
        try
        {
            string aiResponse = await ClaudeAIAgent.SendMessageAsync(userMessage, conversationHistory);
            
            // Remove the thinking message using reference
            RemoveMessageByReference(currentThinkingMessage, "Thinking");
            
            AddMessage(new ChatMessage("Claude", aiResponse));
            conversationHistory.Add(ClaudeMessage.CreateTextMessage("assistant", aiResponse));
            
            suggestionSystem.UpdateSuggestions(aiEnabled, messages);
            ScrollToBottom();
            Repaint();
        }
        catch (System.Exception ex)
        {
            // Remove the thinking message using reference
            RemoveMessageByReference(currentThinkingMessage, "Thinking");
            
            AddMessage(new ChatMessage("System", $"AI Error: {ex.Message}", MessageType.Error));
            ScrollToBottom();
            Repaint();
        }
        finally
        {
            currentThinkingMessage = null;
            isWaitingForAI = false;
            
            // Reset successful compilation state if no compilation is happening
            if (!EditorApplication.isCompiling && !isWaitingForSuccessfulCompilation)
            {
                isWaitingForSuccessfulCompilation = false;
                currentCompilationWaitMessage = null;
            }
            
            // Process any queued error batches after normal AI response
            errorHandler.ProcessQueuedErrors(suggestionSystem, messages);
        }
    }
    
    private void AddMessage(ChatMessage message)
    {
        messages.Add(message);
        ScrollToBottom();
        Repaint();
    }
    
    private void RemoveMessageByReference(ChatMessage messageToRemove, string messageType)
    {
        if (messageToRemove == null)
        {
            Debug.Log($"[ChatWindow] No {messageType} message reference to remove");
            return;
        }
        
        // Try to find by ID first (most robust)
        int indexById = messages.FindIndex(m => m.id == messageToRemove.id);
        if (indexById >= 0)
        {
            messages.RemoveAt(indexById);
            Debug.Log($"[ChatWindow] Removed {messageType} message by ID at index {indexById}");
            return;
        }
        
        // Fallback: try to find by reference equality
        int indexByRef = messages.IndexOf(messageToRemove);
        if (indexByRef >= 0)
        {
            messages.RemoveAt(indexByRef);
            Debug.Log($"[ChatWindow] Removed {messageType} message by reference at index {indexByRef}");
            return;
        }
        
        // Last resort: search by content (but log it as a fallback)
        for (int i = messages.Count - 1; i >= 0; i--)
        {
            if (messages[i].message == messageToRemove.message && 
                messages[i].username == messageToRemove.username &&
                messages[i].type == messageToRemove.type)
            {
                messages.RemoveAt(i);
                Debug.Log($"[ChatWindow] Removed {messageType} message by content match at index {i} (fallback method)");
                return;
            }
        }
        
        Debug.LogWarning($"[ChatWindow] Could not find {messageType} message to remove. Message count: {messages.Count}");
    }
    
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
        
        // Reset all state variables to ensure UI returns to normal
        isWaitingForAI = false;
        isWaitingForSuccessfulCompilation = false;
        currentThinkingMessage = null;
        currentCompilationWaitMessage = null;
        
        AddMessage(new ChatMessage("System", "Chat cleared.", MessageType.System));
    }
    
    private void ScrollToBottom()
    {
        scrollPosition.y = float.MaxValue;
    }
    
    private void CopyConversationToClipboard()
    {
        ChatClipboardManager.CopyConversationToClipboard(messages, consoleCapture.CapturedLogs, consoleCapture.IncludeLogs);
        
        string logInfo = consoleCapture.IncludeLogs ? $" + {consoleCapture.CapturedLogs.Count} console logs" : "";
        AddMessage(new ChatMessage("System", $"Conversation copied to clipboard! ({messages.Count - 1} messages{logInfo})", MessageType.System));
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
        
        // Remove thinking message if it exists
        RemoveMessageByReference(currentThinkingMessage, "Thinking");
        currentThinkingMessage = null;
        
        // Add compilation message
        currentCompilationWaitMessage = new ChatMessage("System", "⚙️ Compiling scripts...", MessageType.System);
        AddMessage(currentCompilationWaitMessage);
        ScrollToBottom();
        Repaint();
    }
    
    private void OnSuccessfulCompilationFinished()
    {
        Debug.Log("[ChatWindow] Successful compilation finished");
        
        // Remove compilation message
        RemoveMessageByReference(currentCompilationWaitMessage, "Compiling");
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
            
            AddMessage(new ChatMessage("System", successMessage, MessageType.System));
        }
        else
        {
            // Handle compilation failure
            if (customSuccessMessageProvider != null)
            {
                string failureMessage = customSuccessMessageProvider(false);
                AddMessage(new ChatMessage("System", failureMessage, MessageType.System));
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