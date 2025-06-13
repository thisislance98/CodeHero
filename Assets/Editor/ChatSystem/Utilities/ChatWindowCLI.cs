using UnityEngine;
using UnityEditor;
using System;
using System.IO;
using System.Collections.Generic;

/// <summary>
/// CLI interface for sending messages to the ChatWindow from terminal commands.
/// Usage: Unity -batchmode -quit -projectPath "/path/to/project" -executeMethod ChatWindowCLI.SendMessage -message "Your message here"
/// </summary>
public static class ChatWindowCLI
{
    private const string TEMP_MESSAGE_FILE = "Temp/chat_cli_message.txt";
    private const string TEMP_RESPONSE_FILE = "Temp/chat_cli_response.txt";
    
    /// <summary>
    /// Send a message to the ChatWindow via command line
    /// Usage: Unity -batchmode -quit -projectPath "/path/to/project" -executeMethod ChatWindowCLI.SendMessage -message "Your message"
    /// </summary>
    public static void SendMessage()
    {
        try
        {
            string message = GetCommandLineArgument("-message");
            if (string.IsNullOrEmpty(message))
            {
                Debug.LogError("[ChatWindowCLI] No message provided. Use -message \"Your message here\"");
                EditorApplication.Exit(1);
                return;
            }
            
            Debug.Log($"[ChatWindowCLI] Processing message: {message}");
            
            // Write message to temp file for the editor instance to pick up
            WriteTempMessage(message);
            
            Debug.Log("[ChatWindowCLI] Message queued successfully");
            EditorApplication.Exit(0);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ChatWindowCLI] Error: {ex.Message}");
            EditorApplication.Exit(1);
        }
    }
    
    /// <summary>
    /// Send a message directly to an open ChatWindow instance (for use when Unity is already running)
    /// </summary>
    public static void SendMessageToOpenWindow()
    {
        try
        {
            string message = GetCommandLineArgument("-message");
            if (string.IsNullOrEmpty(message))
            {
                Debug.LogError("[ChatWindowCLI] No message provided. Use -message \"Your message here\"");
                return;
            }
            
            var window = GetOrCreateChatWindow();
            if (window != null)
            {
                // Use reflection to access the internal SendMessage method
                var method = typeof(ChatWindow).GetMethod("SendMessage", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                
                if (method != null)
                {
                    // Set the input message field
                    var inputField = typeof(ChatWindow).GetField("inputMessage", 
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    
                    if (inputField != null)
                    {
                        inputField.SetValue(window, message);
                        method.Invoke(window, null);
                        Debug.Log($"[ChatWindowCLI] Message sent to ChatWindow: {message}");
                    }
                    else
                    {
                        Debug.LogError("[ChatWindowCLI] Could not access inputMessage field");
                    }
                }
                else
                {
                    Debug.LogError("[ChatWindowCLI] Could not access SendMessage method");
                }
            }
            else
            {
                Debug.LogError("[ChatWindowCLI] Could not find or create ChatWindow");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ChatWindowCLI] Error sending message to open window: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Start monitoring for CLI messages (called by ChatWindow on startup)
    /// </summary>
    public static void StartMonitoring()
    {
        EditorApplication.update += CheckForCLIMessages;
    }
    
    /// <summary>
    /// Stop monitoring for CLI messages
    /// </summary>
    public static void StopMonitoring()
    {
        EditorApplication.update -= CheckForCLIMessages;
    }
    
    private static void CheckForCLIMessages()
    {
        if (File.Exists(TEMP_MESSAGE_FILE))
        {
            try
            {
                string message = File.ReadAllText(TEMP_MESSAGE_FILE);
                File.Delete(TEMP_MESSAGE_FILE);
                
                var window = GetOrCreateChatWindow();
                if (window != null)
                {
                    // Use reflection to access the internal methods
                    var inputField = typeof(ChatWindow).GetField("inputMessage", 
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    var sendMethod = typeof(ChatWindow).GetMethod("SendMessage", 
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    
                    if (inputField != null && sendMethod != null)
                    {
                        inputField.SetValue(window, message);
                        
                        // Log before sending message
                        Debug.Log($"[ChatWindowCLI] About to send CLI message: {message}");
                        
                        sendMethod.Invoke(window, null);
                        
                        Debug.Log($"[ChatWindowCLI] Processed CLI message: {message}");
                        WriteResponse($"Message sent successfully: {message}");
                    }
                    else
                    {
                        WriteResponse($"Error: Could not access ChatWindow methods");
                    }
                }
                else
                {
                    WriteResponse($"Error: Could not find ChatWindow");
                }
            }
            catch (Exception ex)
            {
                WriteResponse($"Error processing CLI message: {ex.Message}");
                Debug.LogError($"[ChatWindowCLI] Error processing message: {ex.Message}");
            }
        }
    }
    
    private static ChatWindow GetOrCreateChatWindow()
    {
        // Try to find existing window first
        var windows = Resources.FindObjectsOfTypeAll<ChatWindow>();
        if (windows.Length > 0)
        {
            return windows[0];
        }
        
        // If no window exists, create one
        ChatWindow window = EditorWindow.GetWindow<ChatWindow>("Chat Window");
        window.minSize = new Vector2(400, 600);
        return window;
    }
    
    private static string GetCommandLineArgument(string argumentName)
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == argumentName && i + 1 < args.Length)
            {
                return args[i + 1];
            }
        }
        return null;
    }
    
    private static void WriteTempMessage(string message)
    {
        Directory.CreateDirectory("Temp");
        File.WriteAllText(TEMP_MESSAGE_FILE, message);
    }
    
    private static void WriteResponse(string response)
    {
        Directory.CreateDirectory("Temp");
        File.WriteAllText(TEMP_RESPONSE_FILE, response);
    }
    
    /// <summary>
    /// Get the last CLI response (useful for shell scripts)
    /// </summary>
    public static void GetLastResponse()
    {
        if (File.Exists(TEMP_RESPONSE_FILE))
        {
            string response = File.ReadAllText(TEMP_RESPONSE_FILE);
            Debug.Log($"[ChatWindowCLI] Last response: {response}");
        }
        else
        {
            Debug.Log("[ChatWindowCLI] No response available");
        }
        EditorApplication.Exit(0);
    }
    
    /// <summary>
    /// Clear any pending CLI messages
    /// </summary>
    public static void ClearMessages()
    {
        if (File.Exists(TEMP_MESSAGE_FILE))
        {
            File.Delete(TEMP_MESSAGE_FILE);
        }
        if (File.Exists(TEMP_RESPONSE_FILE))
        {
            File.Delete(TEMP_RESPONSE_FILE);
        }
        Debug.Log("[ChatWindowCLI] CLI messages cleared");
        EditorApplication.Exit(0);
    }
} 