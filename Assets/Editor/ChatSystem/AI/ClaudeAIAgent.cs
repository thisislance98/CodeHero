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
    
    private static Dictionary<string, string> previousToolContent = new Dictionary<string, string>();
    private static Dictionary<string, DateTime> toolStartTimes = new Dictionary<string, DateTime>();
    private static Dictionary<string, System.Threading.Timer> progressTimers = new Dictionary<string, System.Threading.Timer>();
    
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
            // Log what we're sending to Claude
            ChatWindow.SendDebugMessage($"[ClaudeAI] SendMessageInternalAsync called with userMessage: '{userMessage}'");
            ChatWindow.SendDebugMessage($"[ClaudeAI] ConversationHistory count: {conversationHistory?.Count ?? 0}");
            
            List<ClaudeMessage> messages = new List<ClaudeMessage>();
            if (conversationHistory != null)
            {
                messages.AddRange(conversationHistory);
                // Log the last few messages for context
                for (int i = Math.Max(0, conversationHistory.Count - 2); i < conversationHistory.Count; i++)
                {
                    var msg = conversationHistory[i];
                    var preview = msg.content?[0]?.text?.Substring(0, Math.Min(100, msg.content?[0]?.text?.Length ?? 0)) ?? "[no content]";
                    ChatWindow.SendDebugMessage($"[ClaudeAI] History[{i}]: {msg.role} - {preview}...");
                }
            }
            messages.Add(ClaudeMessage.CreateTextMessage("user", userMessage));
            
            ChatWindow.SendDebugMessage($"[ClaudeAI] Total messages being sent to Claude: {messages.Count}");

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

            ChatWindow.SendDebugMessage($"[ClaudeAI] About to send HTTP request to Claude API");
            string result = await HandleStreamingResponse(content, onTextDelta, currentCancellationSource.Token);
            ChatWindow.SendDebugMessage($"[ClaudeAI] Claude API returned response with length: {result?.Length ?? 0}");
            
            return result;
        }
        catch (TaskCanceledException ex)
        {
            ChatWindow.SendDebugMessage($"[ClaudeAI] TaskCanceledException: {ex.Message}");
            if (currentCancellationSource?.Token.IsCancellationRequested == true)
            {
                onTextDelta?.Invoke("\n⏹️ Streaming stopped by user.\n");
                return "Streaming stopped by user.";
            }
            throw new System.Exception("Request timed out. Please try again.");
        }
        catch (OperationCanceledException ex)
        {
            ChatWindow.SendDebugMessage($"[ClaudeAI] OperationCanceledException: {ex.Message}");
            onTextDelta?.Invoke("\n⏹️ Streaming stopped by user.\n");
            return "Streaming stopped by user.";
        }
        catch (HttpRequestException ex)
        {
            ChatWindow.SendDebugMessage($"[ClaudeAI] HttpRequestException: {ex.Message}");
            throw new System.Exception($"Network error: {ex.Message}");
        }
        catch (JsonException ex)
        {
            ChatWindow.SendDebugMessage($"[ClaudeAI] JsonException: {ex.Message}");
            throw new System.Exception($"Error parsing response: {ex.Message}");
        }
        catch (System.Exception ex)
        {
            ChatWindow.SendDebugMessage($"[ClaudeAI] Unexpected error: {ex.Message}");
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
                            
                            ChatWindow.SendDebugMessage($"Content block start: type={startEvent.content_block.type}");
                            
                            if (startEvent.content_block.type == "tool_use")
                            {
                                ChatWindow.SendDebugMessage($"Tool_use block detected: {startEvent.content_block.name} (id: {startEvent.content_block.id})");
                                
                                // Provide immediate feedback about tool preparation
                                onTextDelta?.Invoke($"\n\n🛠️ **Preparing to execute:** `{startEvent.content_block.name}`");
                                
                                // Track timing for progress updates
                                var toolKey = $"{startEvent.content_block.name}_{startEvent.content_block.id}";
                                toolStartTimes[toolKey] = DateTime.Now;
                                
                                // Show specific feedback for script tools and start progress timer
                                if (IsScriptTool(startEvent.content_block.name))
                                {
                                    onTextDelta?.Invoke($"\n📝 **Generating script content...**");
                                    
                                    // Start a timer to show progress every 5 seconds
                                    var timer = new System.Threading.Timer((_) => {
                                        if (toolStartTimes.ContainsKey(toolKey))
                                        {
                                            var elapsed = DateTime.Now - toolStartTimes[toolKey];
                                            onTextDelta?.Invoke($"\n⏳ **Still generating... ({elapsed.Seconds}s)**");
                                        }
                                    }, null, 5000, 5000); // Start after 5s, repeat every 3s
                                    
                                    progressTimers[toolKey] = timer;
                                }
                                else
                                {
                                    onTextDelta?.Invoke($"\n📋 **Building parameters...**");
                                }
                                
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
            // Stop all progress timers
            foreach (var timer in progressTimers.Values)
            {
                timer?.Dispose();
            }
            progressTimers.Clear();
            toolStartTimes.Clear();
            
            // Close any open code blocks from streaming
            if (previousToolContent.Count > 0)
            {
                onTextDelta?.Invoke("\n```\n");
                previousToolContent.Clear();
            }
            
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
        ChatWindow.SendDebugMessage($"About to execute {toolUses.Count} tools");

        // Execute tools with detailed feedback
        for (int i = 0; i < toolUses.Count; i++)
        {
            var toolUse = toolUses[i];
            
            ChatWindow.SendDebugMessage($"Executing tool {i+1}/{toolUses.Count}: {toolUse.name}");
            
            try
            {
                // Show tool execution start with more context
                onTextDelta?.Invoke($"\n🔧 **Executing {toolUse.name}**");
                
                // Show detailed tool parameters, especially for script tools
                if (toolUse.input != null)
                {
                    try
                    {
                        var inputJson = JsonConvert.SerializeObject(toolUse.input);
                        var inputDict = JsonConvert.DeserializeObject<Dictionary<string, object>>(inputJson);
                        if (inputDict != null && inputDict.Count > 0)
                        {
                            if (IsScriptTool(toolUse.name))
                            {
                                ShowDetailedScriptToolParameters(inputDict, onTextDelta);
                            }
                            else
                            {
                                var paramSummary = string.Join(", ", inputDict.Take(3).Select(kvp => $"{kvp.Key}: {kvp.Value?.ToString()?.Substring(0, Math.Min(30, kvp.Value?.ToString()?.Length ?? 0))}"));
                                if (inputDict.Count > 3) paramSummary += "...";
                                onTextDelta?.Invoke($"\n   📝 Parameters: {paramSummary}");
                            }
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

    private static bool IsScriptTool(string toolName)
    {
        return toolName.Contains("script") || toolName.Contains("create") || toolName.Contains("edit") || 
               toolName.Contains("str_replace") || toolName.Contains("insert") || toolName.Contains("view");
    }

    private static string GetFilePathFromParameters(Dictionary<string, object> parameters)
    {
        foreach (var param in parameters)
        {
            switch (param.Key.ToLower())
            {
                case "filepath":
                case "file_path":
                case "path":
                case "target_file":
                    return param.Value?.ToString() ?? "";
            }
        }
        return "";
    }

    private static void ShowDetailedScriptToolParameters(Dictionary<string, object> parameters, System.Action<string> onTextDelta)
    {
        onTextDelta?.Invoke($"\n   📝 **Final Parameters:**");
        
        foreach (var param in parameters)
        {
            var value = param.Value?.ToString() ?? "";
            
            switch (param.Key.ToLower())
            {
                case "filepath":
                case "file_path":
                case "path":
                case "target_file":
                    onTextDelta?.Invoke($"\n      📄 **File:** `{value}`");
                    break;
                    
                case "content":
                case "code":
                case "new_string":
                    var lines = value.Split('\n');
                    var lineCount = lines.Length;
                    var contentPreview = lineCount > 3 ? 
                        string.Join("\n", lines.Take(3)) + $"\n      ... ({lineCount - 3} more lines)" :
                        value;
                    onTextDelta?.Invoke($"\n      ✏️ **Content:** ({lineCount} line{(lineCount == 1 ? "" : "s")})\n      ```\n{contentPreview}\n      ```");
                    break;
                    
                case "old_string":
                    if (value.Length > 100)
                    {
                        var replacePreview = value.Substring(0, 97) + "...";
                        onTextDelta?.Invoke($"\n      🔍 **Replacing:** `{replacePreview}`");
                    }
                    else
                    {
                        onTextDelta?.Invoke($"\n      🔍 **Replacing:** `{value}`");
                    }
                    break;
                    
                case "line_number":
                case "line":
                    onTextDelta?.Invoke($"\n      📍 **Line:** {value}");
                    break;
                    
                case "instructions":
                case "description":
                    if (value.Length > 80)
                    {
                        var instructionPreview = value.Substring(0, 77) + "...";
                        onTextDelta?.Invoke($"\n      💡 **{param.Key}:** {instructionPreview}");
                    }
                    else
                    {
                        onTextDelta?.Invoke($"\n      💡 **{param.Key}:** {value}");
                    }
                    break;
                    
                default:
                    if (value.Length > 50)
                    {
                        var defaultPreview = value.Substring(0, 47) + "...";
                        onTextDelta?.Invoke($"\n      ⚙️ **{param.Key}:** {defaultPreview}");
                    }
                    else
                    {
                        onTextDelta?.Invoke($"\n      ⚙️ **{param.Key}:** {value}");
                    }
                    break;
            }
        }
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
                            
                            // No complex streaming - just let the timer handle progress
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