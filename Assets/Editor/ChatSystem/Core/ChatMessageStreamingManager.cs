using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;

public class ChatMessageStreamingManager
{
    // Unified message streaming system
    private Queue<MessageQueueEntry> messageQueue = new Queue<MessageQueueEntry>();
    private ChatMessage currentlyStreamingMessage = null;
    private bool isProcessingQueue = false;
    
    // References to parent components
    private ChatWindow parentWindow;
    private List<ChatMessage> messages;
    private List<ClaudeMessage> conversationHistory;
    
    // Events
    public event Action OnMessageProcessingComplete;
    public event Action OnScrollToBottom;
    public event Action OnRepaint;
    
    public ChatMessage CurrentlyStreamingMessage => currentlyStreamingMessage;
    public bool IsProcessingQueue => isProcessingQueue;
    
    public ChatMessageStreamingManager(ChatWindow parent, List<ChatMessage> messagesList, List<ClaudeMessage> conversationHistoryList)
    {
        parentWindow = parent;
        messages = messagesList;
        conversationHistory = conversationHistoryList;
    }
    
    public void QueueMessage(ChatMessage message, bool insertAboveStreaming = false, 
                           System.Action<string> onTextDelta = null, System.Action onComplete = null)
    {
        var entry = new MessageQueueEntry(message, insertAboveStreaming, onTextDelta, onComplete);
        
        messageQueue.Enqueue(entry);
        
        if (!isProcessingQueue)
        {
            ProcessNextMessage();
        }
    }
    
    public void AddMessage(ChatMessage message)
    {
        if (message == null) return;
        
        messages.Add(message);
        OnScrollToBottom?.Invoke();
        OnRepaint?.Invoke();
    }
    
    public void StopStreaming()
    {
        Debug.Log("[ChatMessageStreamingManager] StopStreaming called");
        
        // Stop current AI request
        ClaudeAIAgent.StopStreaming();
        
        // Complete current streaming message if any
        if (currentlyStreamingMessage != null)
        {
            Debug.Log("[ChatMessageStreamingManager] Completing current streaming message");
            
            // Mark the current streaming message as completed with stop indication
            currentlyStreamingMessage.content += "\n⏹️ Streaming stopped by user.";
            currentlyStreamingMessage.isComplete = true;
            currentlyStreamingMessage = null;
            
            OnRepaint?.Invoke();
        }
        
        // Clear the message queue to prevent further processing
        messageQueue.Clear();
        isProcessingQueue = false;
        
        Debug.Log("[ChatMessageStreamingManager] Streaming stopped and queue cleared");
        
        OnMessageProcessingComplete?.Invoke();
    }
    
    private async void ProcessNextMessage()
    {
        if (messageQueue.Count == 0)
        {
            isProcessingQueue = false;
            return;
        }
        
        isProcessingQueue = true;
        
        var entry = messageQueue.Dequeue();
        var message = entry.message;
        
        // Add message to list if not already present and not inserting above streaming
        if (!messages.Contains(message))
        {
            if (entry.requiresInsertionAboveStreaming && currentlyStreamingMessage != null)
            {
                // Find the currently streaming message and insert above it
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
        }
        
        // Handle different message types
        if (message.type == MessageType.System && message.shouldStream)
        {
            await StreamSystemMessage(message);
        }
        else if (message.type == MessageType.Assistant && message.shouldStream)
        {
            currentlyStreamingMessage = message;
            await StreamMessageContent(message, entry.onTextDelta);
            currentlyStreamingMessage = null;
        }
        
        // Call completion callback
        entry.onComplete?.Invoke();
        
        OnScrollToBottom?.Invoke();
        OnRepaint?.Invoke();
        
        // Process next message if any
        EditorApplication.delayCall += ProcessNextMessage;
    }
    
    private async Task StreamSystemMessage(ChatMessage message)
    {
        await StreamSystemMessageAsync(message);
    }
    
    private async Task StreamSystemMessageAsync(ChatMessage message)
    {
        const float charDelay = 0.002f; // Very fast streaming for system messages
        const int charsPerFrame = 3;
        
        string fullContent = message.content;
        message.content = "";
        
        for (int i = 0; i < fullContent.Length; i += charsPerFrame)
        {
            if (ClaudeAIAgent.IsStreaming())
            {
                break; // Stop if AI streaming was cancelled
            }
            
            int endIndex = Math.Min(i + charsPerFrame, fullContent.Length);
            message.content = fullContent.Substring(0, endIndex);
            
            OnRepaint?.Invoke();
            await Task.Delay((int)(charDelay * 1000 * charsPerFrame));
        }
        
        // Ensure full content is set
        message.content = fullContent;
        message.isComplete = true;
        OnRepaint?.Invoke();
    }
    
    private async Task StreamMessageContent(ChatMessage message, System.Action<string> onTextDelta)
    {
        // This method handles streaming for AI assistant messages
        // The actual streaming is handled by the AI agent calling OnUnifiedStreamingTextDelta
        
        // Just wait for the message to be marked as complete
        while (!message.isComplete && !ClaudeAIAgent.IsStreaming())
        {
            await Task.Delay(100);
        }
    }
    
    public void OnUnifiedStreamingTextDelta(ChatMessage message, string textDelta)
    {
        if (message != null)
        {
            if (string.IsNullOrEmpty(message.content))
            {
                message.content = textDelta;
            }
            else
            {
                message.content += textDelta;
            }
            
            OnRepaint?.Invoke();
        }
    }
    
    public async Task ProcessAIResponse(string userMessage)
    {
        try
        {
            var aiMessage = ChatMessage.CreateAssistantMessage("", shouldStream: true);
            QueueMessage(aiMessage, false, null, () => { 
                // AI response completed
                OnMessageProcessingComplete?.Invoke();
            });
            
            // Send to Claude AI with streaming
            await ClaudeAIAgent.SendMessageStreamAsync(userMessage, conversationHistory, 
                (textDelta) => OnUnifiedStreamingTextDelta(aiMessage, textDelta));
            
            // Mark as complete
            aiMessage.isComplete = true;
            
            // Add AI response to conversation history
            conversationHistory.Add(ClaudeMessage.CreateTextMessage("assistant", aiMessage.content));
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ChatMessageStreamingManager] Error in ProcessAIResponse: {ex.Message}");
            var errorMessage = ChatMessage.CreateSystemMessage($"Error communicating with AI: {ex.Message}", MessageType.Error);
            QueueMessage(errorMessage);
            OnMessageProcessingComplete?.Invoke();
        }
    }
}