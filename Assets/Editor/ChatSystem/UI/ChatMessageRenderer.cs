using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class ChatMessageRenderer
{
    private GUIStyle messageStyle;
    private GUIStyle usernameStyle;
    private GUIStyle timestampStyle;
    private GUIStyle systemMessageStyle;
    private bool stylesInitialized = false;
    
    public void InitializeStyles()
    {
        if (stylesInitialized) return;
        
        messageStyle = new GUIStyle(EditorStyles.textArea)
        {
            wordWrap = true,
            richText = true,
            padding = new RectOffset(5, 5, 2, 2),
            fontSize = 14,
            border = new RectOffset(0, 0, 0, 0), // Remove border
            normal = { 
                background = null, // Transparent background
                textColor = EditorGUIUtility.isProSkin ? Color.white : Color.black
            },
            focused = { 
                background = null, // Keep transparent when focused
                textColor = EditorGUIUtility.isProSkin ? Color.white : Color.black
            }
        };
        
        usernameStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            richText = true,
            padding = new RectOffset(5, 5, 2, 0),
            fontSize = 13
        };
        
        timestampStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.UpperRight,
            padding = new RectOffset(5, 5, 0, 2),
            fontSize = 10
        };
        
        systemMessageStyle = new GUIStyle(EditorStyles.textArea)
        {
            wordWrap = true,
            richText = true,
            fontStyle = FontStyle.Italic,
            padding = new RectOffset(5, 5, 2, 2),
            fontSize = 14,
            border = new RectOffset(0, 0, 0, 0), // Remove border
            normal = { 
                background = null, // Transparent background
                textColor = Color.cyan
            },
            focused = { 
                background = null, // Keep transparent when focused
                textColor = Color.cyan
            }
        };
        
        stylesInitialized = true;
    }
    
    public void DrawMessage(ChatMessage message)
    {
        InitializeStyles();
        
        EditorGUILayout.BeginVertical(GUI.skin.box);
        
        // Username and timestamp row
        EditorGUILayout.BeginHorizontal();
        
        Color originalColor = GUI.color;
        
        switch (message.type)
        {
            case MessageType.System:
                GUI.color = Color.cyan;
                break;
            case MessageType.Warning:
                GUI.color = Color.yellow;
                break;
            case MessageType.Error:
                GUI.color = Color.red;
                break;
            default:
                GUI.color = Color.white;
                break;
        }
        
        if (message.type == MessageType.System)
        {
            GUILayout.Label($"<b>[{message.username}]</b>", usernameStyle);
        }
        else
        {
            GUILayout.Label($"<b>{message.username}:</b>", usernameStyle);
        }
        
        GUILayout.FlexibleSpace();
        GUILayout.Label(message.timestamp, timestampStyle);
        
        GUI.color = originalColor;
        EditorGUILayout.EndHorizontal();
        
        // Message content - Use TextArea for proper text selection
        GUIStyle styleToUse = message.type == MessageType.System ? systemMessageStyle : messageStyle;
        
        // Calculate height needed for the text
        float textHeight = styleToUse.CalcHeight(new GUIContent(message.message), EditorGUIUtility.currentViewWidth - 40);
        textHeight = Mathf.Max(textHeight, styleToUse.lineHeight);
        
        // Use TextArea for selectable text (but make it read-only by ignoring changes)
        GUI.enabled = true; // Ensure it's enabled for selection
        string _ = EditorGUILayout.TextArea(message.message, styleToUse, 
            GUILayout.Height(textHeight), 
            GUILayout.ExpandWidth(true));
        // We don't use the returned value to make it effectively read-only
        
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(2);
    }
    
    public void DrawMessagesArea(List<ChatMessage> messages, ref Vector2 scrollPosition)
    {
        // Set black background for messages area
        Color originalBackgroundColor = GUI.backgroundColor;
        GUI.backgroundColor = Color.black;
        
        // Messages scroll view
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, 
            GUILayout.ExpandHeight(true), GUILayout.MinHeight(500));
        
        foreach (var message in messages)
        {
            DrawMessage(message);
        }
        
        EditorGUILayout.EndScrollView();
        
        // Restore original background color
        GUI.backgroundColor = originalBackgroundColor;
    }
} 