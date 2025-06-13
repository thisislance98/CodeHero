using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

public class ChatSuggestionSystem
{
    private string[] currentSuggestions;
    private GUIStyle suggestionButtonStyle;
    private bool stylesInitialized = false;
    
    public string[] CurrentSuggestions => currentSuggestions;
    
    public void UpdateSuggestions(bool aiEnabled, List<ChatMessage> messages)
    {
        List<string> suggestions = new List<string>();
        
        // Base suggestions always available
        if (aiEnabled)
        {
            suggestions.AddRange(new string[]
            {
                "Create a sample script",
                "Create a cube",
                "Create a sphere", 
                "List all GameObjects",
                "Help me with Unity scripting"
            });
        }
        
        // Context-based suggestions
        if (messages.Count > 0)
        {
            var lastMessage = messages[messages.Count - 1];
            
            // If last message was about creating objects, suggest related actions
            if (lastMessage.message.ToLower().Contains("created") && lastMessage.message.ToLower().Contains("gameobject"))
            {
                suggestions.Insert(0, "Add a component to it");
                suggestions.Insert(1, "Move it to position 0,5,0");
            }
            
            // If there was an error, suggest help
            if (lastMessage.type == MessageType.Error)
            {
                suggestions.Insert(0, "What went wrong?");
                suggestions.Insert(1, "Try a different approach");
            }
        }
        
        // Add utility suggestions
        suggestions.AddRange(new string[]
        {
            "/help",
            "/clear"
        });
        
        // Limit to 6 suggestions to avoid UI clutter
        if (suggestions.Count > 6)
        {
            suggestions = suggestions.GetRange(0, 6);
        }
        
        currentSuggestions = suggestions.ToArray();
    }
    
    public void DrawSuggestionButtons(Rect windowRect, bool isWaitingForAI, System.Action<string> onSuggestionClicked)
    {
        if (currentSuggestions == null || currentSuggestions.Length == 0)
            return;
            
        InitializeSuggestionButtonStyle();
        
        try
        {
            // Calculate how many buttons can fit per row (roughly)
            float windowWidth = windowRect.width - 20; // Account for margins
            float buttonWidth = 120;
            int buttonsPerRow = Mathf.Max(1, (int)(windowWidth / buttonWidth));
            
            bool originalGUIEnabled = GUI.enabled;
            
            for (int i = 0; i < currentSuggestions.Length; i += buttonsPerRow)
            {
                EditorGUILayout.BeginHorizontal();
                
                try
                {
                    for (int j = i; j < Mathf.Min(i + buttonsPerRow, currentSuggestions.Length); j++)
                    {
                        string suggestion = currentSuggestions[j];
                        
                        GUI.enabled = !isWaitingForAI && originalGUIEnabled;
                        if (GUILayout.Button(suggestion, suggestionButtonStyle, GUILayout.MaxWidth(buttonWidth)))
                        {
                            onSuggestionClicked?.Invoke(suggestion);
                        }
                    }
                    
                    GUILayout.FlexibleSpace();
                }
                finally
                {
                    EditorGUILayout.EndHorizontal();
                }
            }
            
            GUI.enabled = originalGUIEnabled;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Error drawing suggestion buttons: {ex.Message}");
        }
    }
    
    private void InitializeSuggestionButtonStyle()
    {
        if (stylesInitialized) return;
        
        suggestionButtonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 10,
            padding = new RectOffset(6, 6, 3, 3),
            margin = new RectOffset(2, 2, 1, 1),
            normal = {
                textColor = EditorGUIUtility.isProSkin ? Color.white : Color.black,
                background = EditorGUIUtility.isProSkin ? 
                    EditorGUIUtility.Load("builtin skins/darkskin/images/btn.png") as Texture2D :
                    EditorGUIUtility.Load("builtin skins/lightskin/images/btn.png") as Texture2D
            },
            hover = {
                textColor = EditorGUIUtility.isProSkin ? Color.cyan : Color.blue
            },
            wordWrap = false,
            alignment = TextAnchor.MiddleCenter
        };
        
        stylesInitialized = true;
    }
} 