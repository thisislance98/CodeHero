using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using System.Net.Http;
using System.Text;
using System;
using Newtonsoft.Json;
using System.IO;
using System.Linq;

public class ClaudeAIAgent
{
    private static readonly string API_KEY = GetApiKey();
    private static readonly string API_URL = "https://api.anthropic.com/v1/messages";
    
    private static HttpClient httpClient = new HttpClient();
    private static CancellationTokenSource currentCancellationSource;
    
    static ClaudeAIAgent()
    {
        httpClient.DefaultRequestHeaders.Add("x-api-key", API_KEY);
        httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
    }
    
    public static void StopStreaming()
    {
        if (currentCancellationSource != null && !currentCancellationSource.Token.IsCancellationRequested)
        {
            currentCancellationSource.Cancel();
        }
    }
    
    public static bool IsStreaming()
    {
        return currentCancellationSource != null && !currentCancellationSource.Token.IsCancellationRequested;
    }
    
    [MenuItem("Claude AI/Stop Streaming")]
    public static void StopStreamingMenuItem()
    {
        StopStreaming();
    }
    
    private static string GetApiKey()
    {
        string apiKey = Environment.GetEnvironmentVariable("CLAUDE_API_KEY");
        
        if (!string.IsNullOrEmpty(apiKey))
        {
            return apiKey;
        }
        
        string configPath = Path.Combine(Application.dataPath, "Editor", "ChatSystem", "Configuration", "claude_config.txt");
        if (File.Exists(configPath))
        {
            try
            {
                string key = File.ReadAllText(configPath).Trim();
                if (!string.IsNullOrEmpty(key))
                {
                    return key;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ClaudeAI] Error reading config file: {ex.Message}");
            }
        }
        
        Debug.LogError("[ClaudeAI] No API key found! Please either:\n" +
                      "1. Set CLAUDE_API_KEY environment variable, OR\n" +
                      "2. Create Assets/Editor/ChatSystem/Configuration/claude_config.txt with your API key");
        
        return "YOUR_CLAUDE_API_KEY_HERE";
    }
    
    public static async Task<string> SendMessageAsync(string userMessage, List<ClaudeMessage> conversationHistory = null, System.Action<string> onTextDelta = null)
    {
        return await SendMessageInternalAsync(userMessage, conversationHistory, onTextDelta);
    }

    public static async Task<string> SendMessageStreamAsync(string userMessage, List<ClaudeMessage> conversationHistory = null, System.Action<string> onTextDelta = null)
    {
        return await SendMessageAsync(userMessage, conversationHistory, onTextDelta);
    }

    private static async Task<string> SendMessageInternalAsync(string userMessage, List<ClaudeMessage> conversationHistory = null, System.Action<string> onTextDelta = null)
    {
        if (string.IsNullOrEmpty(API_KEY))
        {
            throw new System.Exception("Claude API key not found. Please set CLAUDE_API_KEY environment variable or create claude_config.txt file.");
        }

        currentCancellationSource?.Dispose();
        currentCancellationSource = new CancellationTokenSource();

        try
        {
            List<ClaudeMessage> messages = new List<ClaudeMessage>();
            if (conversationHistory != null)
            {
                messages.AddRange(conversationHistory);
            }
            messages.Add(ClaudeMessage.CreateTextMessage("user", userMessage));

            var request = new ClaudeRequest
            {
                model = "claude-sonnet-4-20250514",
                max_tokens = 8192,
                messages = messages,
                tools = UnityTools.GetUnityTools()
            };

            request.system = new List<ClaudeSystemMessage>
            {
                new ClaudeSystemMessage
                {
                    text = SystemPrompts.GetCodeHeroSystemPrompt()
                }
            };

            var jsonSettings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                ContractResolver = new ClaudeContentBlockContractResolver()
            };

            string jsonRequest = JsonConvert.SerializeObject(request, jsonSettings);
            var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

            return await HandleStreamingResponse(content, onTextDelta, currentCancellationSource.Token);
        }
        catch (TaskCanceledException ex)
        {
            if (currentCancellationSource?.Token.IsCancellationRequested == true)
            {
                onTextDelta?.Invoke("\n⏹️ Streaming stopped by user.\n");
                return "Streaming stopped by user.";
            }
            throw new System.Exception("Request timed out. Please try again.");
        }
        catch (OperationCanceledException)
        {
            onTextDelta?.Invoke("\n⏹️ Streaming stopped by user.\n");
            return "Streaming stopped by user.";
        }
        catch (HttpRequestException ex)
        {
            throw new System.Exception($"Network error: {ex.Message}");
        }
        catch (JsonException ex)
        {
            throw new System.Exception($"Error parsing response: {ex.Message}");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[ClaudeAI] Unexpected error: {ex.Message}");
            throw;
        }
        finally
        {
            if (currentCancellationSource != null)
            {
                currentCancellationSource.Dispose();
                currentCancellationSource = null;
            }
        }
    }

    private static async Task<string> HandleStreamingResponse(StringContent content, System.Action<string> onTextDelta, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, API_URL)
        {
            Content = content
        };
        
        var requestBody = await content.ReadAsStringAsync();
        var requestData = JsonConvert.DeserializeObject<ClaudeRequest>(requestBody);
        requestData.stream = true;
        
        var streamContent = new StringContent(JsonConvert.SerializeObject(requestData), Encoding.UTF8, "application/json");
        request.Content = streamContent;

        var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new System.Exception($"Claude API error: {response.StatusCode} - {errorContent}");
        }

        return await ProcessStreamingResponse(response, onTextDelta, requestData.messages, cancellationToken);
    }

    private static async Task<string> ProcessStreamingResponse(HttpResponseMessage response, System.Action<string> onTextDelta, List<ClaudeMessage> originalMessages = null, CancellationToken cancellationToken = default)
    {
        var fullResponse = new StringBuilder();
        var contentBlocks = new List<ClaudeContentBlock>();
        var toolUses = new List<ClaudeToolUse>();
        var stopReason = "";
        
        using (var stream = await response.Content.ReadAsStreamAsync())
        using (var reader = new System.IO.StreamReader(stream))
        {
            string line;
            while ((line = await reader.ReadLineAsync()) != null)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data: "))
                    continue;

                var data = line.Substring(6);
                
                if (data == "[DONE]")
                    break;

                try
                {
                    var eventData = JsonConvert.DeserializeObject<StreamEvent>(data);
                    
                    switch (eventData.type)
                    {
                        case "content_block_start":
                            var startEvent = JsonConvert.DeserializeObject<ContentBlockStartEvent>(data);
                            contentBlocks.Add(startEvent.content_block);
                            
                            if (startEvent.content_block.type == "tool_use")
                            {
                                var toolUse = new ClaudeToolUse
                                {
                                    id = startEvent.content_block.id,
                                    name = startEvent.content_block.name,
                                    input = new { } // Initialize with empty object instead of null
                                };
                                toolUses.Add(toolUse);
                            }
                            break;

                        case "content_block_delta":
                            var deltaEvent = JsonConvert.DeserializeObject<ContentBlockDeltaEvent>(data);
                            await ProcessContentBlockDelta(deltaEvent, contentBlocks, toolUses, onTextDelta, fullResponse);
                            break;

                        case "message_delta":
                            var messageDelta = JsonConvert.DeserializeObject<MessageDeltaEvent>(data);
                            if (messageDelta.delta?.stop_reason != null)
                            {
                                stopReason = messageDelta.delta.stop_reason;
                            }
                            break;
                    }
                }
                catch (JsonException)
                {
                    // Ignore malformed JSON events
                }
            }
        }

        if (toolUses.Count > 0)
        {
            // Immediate feedback that tool execution is about to begin
            onTextDelta?.Invoke($"\n\n🛠️ **Starting tool execution** ({toolUses.Count} tool{(toolUses.Count > 1 ? "s" : "")} to execute)...");
            return await ProcessToolUsesAndContinueConversation(toolUses, fullResponse.ToString(), stopReason, onTextDelta, originalMessages, cancellationToken);
        }

        return fullResponse.ToString();
    }

    private static async Task<string> ProcessToolUsesAndContinueConversation(List<ClaudeToolUse> toolUses, string currentResponse, string stopReason, System.Action<string> onTextDelta, List<ClaudeMessage> originalConversationHistory, CancellationToken cancellationToken = default)
    {
        Debug.Log($"[ClaudeAI] ProcessToolUsesAndContinueConversation: Starting with {toolUses.Count} tools");
        
        var conversationMessages = new List<ClaudeMessage>();
        if (originalConversationHistory != null)
        {
            conversationMessages.AddRange(originalConversationHistory);
        }

        // Create assistant message with tool uses
        var assistantContentBlocks = new List<ClaudeContentBlock>();
        
        // Add text content if present
        if (!string.IsNullOrEmpty(currentResponse))
        {
            assistantContentBlocks.Add(new ClaudeContentBlock
            {
                type = "text",
                text = currentResponse
            });
        }
        
        // Add tool use blocks
        foreach (var toolUse in toolUses)
        {
            assistantContentBlocks.Add(new ClaudeContentBlock
            {
                type = "tool_use",
                id = toolUse.id,
                name = toolUse.name,
                input = toolUse.input
            });
        }
        
        // Add assistant message with tool uses
        Debug.Log($"[ClaudeAI] Adding assistant message with {assistantContentBlocks.Count} content blocks");
        conversationMessages.Add(new ClaudeMessage
        {
            role = "assistant",
            content = assistantContentBlocks
        });
        
        Debug.Log($"[ClaudeAI] About to execute {toolUses.Count} tools");

        // Execute tools with detailed feedback
        for (int i = 0; i < toolUses.Count; i++)
        {
            var toolUse = toolUses[i];
            
            try
            {
                // Show tool execution start with more context
                onTextDelta?.Invoke($"\n🔧 **Executing {toolUse.name}**");
                
                // Show tool parameters if available
                if (toolUse.input != null)
                {
                    try
                    {
                        var inputJson = JsonConvert.SerializeObject(toolUse.input);
                        var inputDict = JsonConvert.DeserializeObject<Dictionary<string, object>>(inputJson);
                        if (inputDict != null && inputDict.Count > 0)
                        {
                            var paramSummary = string.Join(", ", inputDict.Take(3).Select(kvp => $"{kvp.Key}: {kvp.Value?.ToString()?.Substring(0, Math.Min(30, kvp.Value?.ToString()?.Length ?? 0))}"));
                            if (inputDict.Count > 3) paramSummary += "...";
                            onTextDelta?.Invoke($"\n   📝 Parameters: {paramSummary}");
                        }
                    }
                    catch
                    {
                        // If parameter parsing fails, continue without showing them
                    }
                }
                
                Debug.Log($"[ClaudeAI] Executing tool: {toolUse.name}");
                var result = UnityTools.ExecuteTool(toolUse);
                Debug.Log($"[ClaudeAI] Tool {toolUse.name} completed successfully");
                
                // Show success with more detailed result
                if (!string.IsNullOrEmpty(result))
                {
                    var resultPreview = result;
                    if (resultPreview.Length > 120)
                    {
                        resultPreview = resultPreview.Substring(0, 120) + "...";
                    }
                    
                    // Split long results into multiple lines for better readability
                    if (resultPreview.Contains("\n"))
                    {
                        var lines = resultPreview.Split('\n');
                        onTextDelta?.Invoke($"\n   ✅ **Success:**\n      {lines[0]}");
                        if (lines.Length > 1)
                        {
                            onTextDelta?.Invoke($"\n      {string.Join("\n      ", lines.Skip(1).Take(2))}");
                            if (lines.Length > 3) onTextDelta?.Invoke("\n      ...");
                        }
                    }
                    else
                    {
                        onTextDelta?.Invoke($"\n   ✅ **Success:** {resultPreview}");
                    }
                }
                else
                {
                    onTextDelta?.Invoke($"\n   ✅ **Completed** (no output)");
                }
                
                conversationMessages.Add(ClaudeMessage.CreateToolResultMessage(toolUse.id, result));
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ClaudeAI] Tool {toolUse.name} failed: {ex.Message}");
                var errorResult = $"Tool execution failed: {ex.Message}";
                onTextDelta?.Invoke($"\n   ❌ **Error:** {ex.Message}");
                
                conversationMessages.Add(ClaudeMessage.CreateToolResultMessage(toolUse.id, errorResult));
            }
        }

        if (stopReason == "tool_use")
        {
            onTextDelta?.Invoke("\n\n");
            return await SendContinuationStreamAsync(conversationMessages, onTextDelta, cancellationToken);
        }

        return currentResponse;
    }

    private static async Task<string> SendContinuationStreamAsync(List<ClaudeMessage> conversationMessages, System.Action<string> onTextDelta, CancellationToken cancellationToken)
    {
        var request = new ClaudeRequest
        {
            model = "claude-sonnet-4-20250514",
            max_tokens = 8192,
            messages = conversationMessages,
            tools = UnityTools.GetUnityTools(),
            stream = true
        };

        request.system = new List<ClaudeSystemMessage>
        {
            new ClaudeSystemMessage
            {
                text = SystemPrompts.GetCodeHeroSystemPrompt()
            }
        };

        var jsonSettings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            ContractResolver = new ClaudeContentBlockContractResolver()
        };

        string jsonRequest = JsonConvert.SerializeObject(request, jsonSettings);
        var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, API_URL)
        {
            Content = content
        };

        var response = await httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new System.Exception($"Claude API error: {response.StatusCode} - {errorContent}");
        }

        return await ProcessStreamingResponse(response, onTextDelta, conversationMessages, cancellationToken);
    }

    private static async Task ProcessContentBlockDelta(ContentBlockDeltaEvent delta, List<ClaudeContentBlock> contentBlocks, List<ClaudeToolUse> toolUses, System.Action<string> onTextDelta, StringBuilder fullResponse)
    {
        if (delta.index < 0 || delta.index >= contentBlocks.Count)
            return;

        var contentBlock = contentBlocks[delta.index];

        switch (delta.delta.type)
        {
            case "text_delta":
                if (!string.IsNullOrEmpty(delta.delta.text))
                {
                    onTextDelta?.Invoke(delta.delta.text);
                    fullResponse.Append(delta.delta.text);
                }
                break;

            case "input_json_delta":
                if (contentBlock.type == "tool_use")
                {
                    if (contentBlock.partial_input == null)
                    {
                        contentBlock.partial_input = "";
                    }
                    
                    contentBlock.partial_input += delta.delta.partial_json;
                    
                    try
                    {
                        var input = JsonConvert.DeserializeObject(contentBlock.partial_input);
                        if (input != null)
                        {
                            var existingIndex = toolUses.FindIndex(t => t.id == contentBlock.id);
                            if (existingIndex >= 0)
                            {
                                toolUses[existingIndex].input = input;
                            }
                            else
                            {
                                var toolUse = new ClaudeToolUse
                                {
                                    id = contentBlock.id,
                                    name = contentBlock.name,
                                    input = input
                                };
                                toolUses.Add(toolUse);
                            }
                        }
                    }
                    catch (JsonException)
                    {
                        // Still building the JSON, continue
                    }
                }
                break;
        }
    }
}