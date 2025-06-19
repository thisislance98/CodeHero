using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class ChatWindowUI
{
    private ChatMessageRenderer messageRenderer;
    private ChatSuggestionSystem suggestionSystem;
    private Vector2 scrollPosition;
    
    // UI state
    private string lastSystemMessage = "";
    private double lastSystemMessageTime = 0;
    
    // References to parent window
    private ChatWindow parentWindow;
    
    public ChatWindowUI(ChatWindow parent)
    {
        parentWindow = parent;
        messageRenderer = new ChatMessageRenderer();
        suggestionSystem = new ChatSuggestionSystem();
    }
    
    public ChatSuggestionSystem GetSuggestionSystem() => suggestionSystem;
    
    public void UpdateSystemMessage(string message)
    {
        lastSystemMessage = message;
        lastSystemMessageTime = EditorApplication.timeSinceStartup;
    }
    
    public void DrawUI(List<ChatMessage> messages, ref string inputMessage, bool aiEnabled, bool isWaitingForAI, 
                       ChatMessage currentlyStreamingMessage, bool autoFixEnabled, bool autoCompilationEnabled, 
                       bool showTokenUsage, Rect windowPosition)
    {
        EditorGUILayout.BeginVertical(GUILayout.ExpandHeight(true));
        
        DrawHeader(aiEnabled, autoFixEnabled, autoCompilationEnabled, showTokenUsage);
        
        // Calculate available height for messages area
        float headerHeight = 25f;
        float baseInputHeight = 100f;
        float suggestionsHeight = (suggestionSystem.CurrentSuggestions != null && suggestionSystem.CurrentSuggestions.Length > 0) ? 60f : 0f;
        float helpTextHeight = 40f;
        float padding = 15f;
        
        float totalInputAreaHeight = baseInputHeight + suggestionsHeight + helpTextHeight + padding;
        float availableHeight = windowPosition.height - headerHeight - totalInputAreaHeight;
        availableHeight = Mathf.Max(availableHeight, 200f);
        
        DrawMessagesArea(messages, availableHeight);
        
        var result = DrawInputArea(ref inputMessage, aiEnabled, isWaitingForAI, currentlyStreamingMessage, windowPosition);
        
        EditorGUILayout.EndVertical();
        
        // Handle UI events
        if (result.shouldSend)
        {
            parentWindow.SendMessage(inputMessage);
        }
        if (result.shouldStop)
        {
            parentWindow.StopStreaming();
        }
        if (result.suggestionSent != null)
        {
            parentWindow.SendSuggestion(result.suggestionSent);
        }
    }
    
    private void DrawHeader(bool aiEnabled, bool autoFixEnabled, bool autoCompilationEnabled, bool showTokenUsage)
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        
        GUILayout.Label("Chat Window", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        
        // Auto-fix toggle
        bool newAutoFixEnabled = GUILayout.Toggle(autoFixEnabled, "Auto Fix", EditorStyles.toolbarButton, GUILayout.Width(70));
        if (newAutoFixEnabled != autoFixEnabled)
        {
            string statusMessage = newAutoFixEnabled ? 
                "✅ Auto Fix enabled - compilation errors will be automatically fixed" : 
                "⚠️ Auto Fix disabled - compilation errors will only be detected";
            parentWindow.OnAutoFixToggled(newAutoFixEnabled, statusMessage);
        }
        
        // Auto-compilation toggle
        bool newAutoCompilationEnabled = GUILayout.Toggle(autoCompilationEnabled, "Auto Compile", EditorStyles.toolbarButton, GUILayout.Width(90));
        if (newAutoCompilationEnabled != autoCompilationEnabled)
        {
            parentWindow.OnAutoCompilationToggled(newAutoCompilationEnabled);
        }
        
        // Token usage toggle
        bool newShowTokenUsage = GUILayout.Toggle(showTokenUsage, "Token Usage", EditorStyles.toolbarButton, GUILayout.Width(90));
        if (newShowTokenUsage != showTokenUsage)
        {
            string statusMessage = newShowTokenUsage ? 
                "📊 Token usage display enabled - costs will be shown after each interaction" : 
                "📊 Token usage display disabled";
            parentWindow.OnTokenUsageToggled(newShowTokenUsage, statusMessage);
        }
        
        // Streaming indicator (always enabled)
        GUI.enabled = false;
        GUILayout.Toggle(true, "Streaming ✓", EditorStyles.toolbarButton, GUILayout.Width(80));
        GUI.enabled = true;
        
        if (GUILayout.Button("Copy", EditorStyles.toolbarButton, GUILayout.Width(50)))
        {
            parentWindow.CopyConversationToClipboard();
        }
        
        if (GUILayout.Button("Clear", EditorStyles.toolbarButton, GUILayout.Width(50)))
        {
            parentWindow.ClearMessages();
        }
        
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space();
    }
    
    private void DrawMessagesArea(List<ChatMessage> messages, float availableHeight)
    {
        EditorGUILayout.BeginVertical(GUILayout.Height(availableHeight), GUILayout.ExpandWidth(true));
        messageRenderer.DrawMessagesArea(messages, ref scrollPosition);
        EditorGUILayout.EndVertical();
    }
    
    private InputAreaResult DrawInputArea(ref string inputMessage, bool aiEnabled, bool isWaitingForAI, 
                                         ChatMessage currentlyStreamingMessage, Rect windowPosition)
    {
        var result = new InputAreaResult();
        
        EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
        EditorGUILayout.Space(2);
        
        DrawSystemMessageLabel();
        
        EditorGUILayout.BeginHorizontal();
        
        GUI.SetNextControlName("MessageInput");
        
        // Handle Enter key
        bool shouldSend = HandleInputKeyEvents();
        
        EditorGUILayout.BeginVertical();
        EditorGUILayout.LabelField("Message:");
        inputMessage = EditorGUILayout.TextArea(inputMessage, GUILayout.Height(60), GUILayout.ExpandWidth(true));
        EditorGUILayout.EndVertical();
        
        // Show appropriate button based on streaming state
        bool sendButtonPressed = false;
        bool stopButtonPressed = false;
        
        if (isWaitingForAI || currentlyStreamingMessage != null)
        {
            stopButtonPressed = GUILayout.Button("⏹️ Stop", GUILayout.Width(60), GUILayout.Height(60));
        }
        else
        {
            GUI.enabled = !isWaitingForAI;
            sendButtonPressed = GUILayout.Button("Send", GUILayout.Width(60), GUILayout.Height(60));
            GUI.enabled = true;
        }
        
        if ((shouldSend || sendButtonPressed) && !string.IsNullOrEmpty(inputMessage.Trim()) && !isWaitingForAI)
        {
            result.shouldSend = true;
        }
        
        if (stopButtonPressed)
        {
            result.shouldStop = true;
        }
        
        EditorGUILayout.EndHorizontal();
        
        result.suggestionSent = DrawSuggestions(windowPosition, isWaitingForAI);
        DrawHelpText(aiEnabled);
        
        EditorGUILayout.EndVertical();
        
        return result;
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
    
    private string DrawSuggestions(Rect windowPosition, bool isWaitingForAI)
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
            return suggestionSystem.DrawSuggestionButtons(windowPosition, isWaitingForAI, null);
        }
        
        return null;
    }
    
    private void DrawSystemMessageLabel()
    {
        if (!string.IsNullOrEmpty(lastSystemMessage))
        {
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
    
    private void DrawHelpText(bool aiEnabled)
    {
        EditorGUILayout.Space(2);
        string helpText = aiEnabled ? 
            "Chat with Claude AI with real-time streaming! Ask it to create scripts, objects, or manipulate your Unity scene. Press Enter to send, Shift+Enter for new line." :
            "AI is disabled. Enable it to chat with Claude. Press Enter or click Send to send messages.";
        EditorGUIUtility.AddCursorRect(GUILayoutUtility.GetLastRect(), MouseCursor.Arrow);
        EditorGUILayout.HelpBox(helpText, UnityEditor.MessageType.Info);
    }
    
    public void ScrollToBottom()
    {
        scrollPosition.y = float.MaxValue;
    }
    
    public void UpdateSuggestions(bool aiEnabled, List<ChatMessage> messages, bool isWaitingOrStreaming)
    {
        suggestionSystem.UpdateSuggestions(aiEnabled, messages, isWaitingOrStreaming);
    }
}

public class InputAreaResult
{
    public bool shouldSend = false;
    public bool shouldStop = false;
    public string suggestionSent = null;
} 