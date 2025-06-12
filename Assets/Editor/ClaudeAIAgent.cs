using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Net.Http;
using System.Text;
using System;
using Newtonsoft.Json;
using System.IO;
using UnityEditor.SceneManagement;
using System.Linq;

[System.Serializable]
public class ClaudeMessage
{
    public string role;
    public List<ClaudeContentBlock> content;
    
    // Legacy constructor for backward compatibility
    public ClaudeMessage()
    {
        content = new List<ClaudeContentBlock>();
    }
    
    // Constructor that creates a simple text message
    public static ClaudeMessage CreateTextMessage(string role, string text)
    {
        var contentBlock = new ClaudeContentBlock();
        contentBlock.type = "text";
        contentBlock.text = text;
        // Explicitly ensure other fields remain null
        contentBlock.id = null;
        contentBlock.name = null;
        contentBlock.input = null;
        contentBlock.tool_use_id = null;
        contentBlock.content = null;
        
        var message = new ClaudeMessage
        {
            role = role,
            content = new List<ClaudeContentBlock> { contentBlock }
        };
        return message;
    }
    
    // Constructor that creates a tool result message
    public static ClaudeMessage CreateToolResultMessage(string toolUseId, string result)
    {
        var message = new ClaudeMessage
        {
            role = "user",
            content = new List<ClaudeContentBlock>
            {
                new ClaudeContentBlock 
                { 
                    type = "tool_result", 
                    tool_use_id = toolUseId,
                    content = result
                }
            }
        };
        return message;
    }
}

[System.Serializable]
public class ClaudeContentBlock
{
    public string type;
    
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string text = null;
    
    // For tool_use type
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string id = null;
    
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string name = null;
    
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public object input = null;
    
    // For tool_result type
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string tool_use_id = null;
    
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string content = null;
}

[System.Serializable]
public class ClaudeSystemMessage
{
    public string type = "text";
    public string text;
}

[System.Serializable]
public class ClaudeRequest
{
    public string model = "claude-sonnet-4-20250514";
    public int max_tokens = 4096;
    
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public List<ClaudeSystemMessage> system;
    
    public List<ClaudeMessage> messages;
    public List<ClaudeTool> tools;
}

[System.Serializable]
public class ClaudeResponse
{
    public string id;
    public string type;
    public string role;
    public List<ClaudeContentBlock> content;
}

[System.Serializable]
public class ClaudeToolUse
{
    public string id;
    public string name;
    public object input;
}

[System.Serializable]
public class ClaudeTool
{
    public string name;
    public string description;
    public ClaudeToolInputSchema input_schema;
}

[System.Serializable]
public class ClaudeToolInputSchema
{
    public string type = "object";
    public Dictionary<string, ClaudeToolProperty> properties;
    public List<string> required;
    public bool additionalProperties = false;
}

[System.Serializable]
public class ClaudeToolProperty
{
    public string type;
    public string description;
    
    [JsonProperty("enum", NullValueHandling = NullValueHandling.Ignore)]
    public List<string> enumValues;
}

public class ClaudeAIAgent
{
    private static readonly string API_KEY = GetApiKey();
    private static readonly string API_URL = "https://api.anthropic.com/v1/messages";
    
    private static HttpClient httpClient = new HttpClient();
    
    static ClaudeAIAgent()
    {
        httpClient.DefaultRequestHeaders.Add("x-api-key", API_KEY);
        httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
    }
    
    private static string GetApiKey()
    {
        // First try to get from environment variable
        string apiKey = Environment.GetEnvironmentVariable("CLAUDE_API_KEY");
        
        if (!string.IsNullOrEmpty(apiKey))
        {
            return apiKey;
        }
        
        // Fallback to config file that won't be committed to repo
        string configPath = Path.Combine(Application.dataPath, "Editor", "claude_config.txt");
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
        
        // If no config found, provide helpful instructions
        Debug.LogError("[ClaudeAI] No API key found! Please either:\n" +
                      "1. Set CLAUDE_API_KEY environment variable, OR\n" +
                      "2. Create Assets/Editor/claude_config.txt with your API key");
        
        return "YOUR_CLAUDE_API_KEY_HERE";
    }
    
    public static async Task<string> SendMessageAsync(string userMessage, List<ClaudeMessage> conversationHistory = null, bool isErrorFix = false)
    {
        try
        {
            var messages = new List<ClaudeMessage>();
            var systemMessages = new List<ClaudeSystemMessage>();
            
            // Set system prompt for error fixing if this is an error fix request
            if (isErrorFix)
            {
                systemMessages.Add(new ClaudeSystemMessage
                {
                    text = @"You are a Unity C# error fixing assistant. When analyzing Unity console errors:

1. IDENTIFY the root cause of the error from the error message and stack trace
2. USE the read_script tool to examine the problematic script if needed  
3. PROVIDE a corrected version using the edit_script tool
4. EXPLAIN what was wrong and how you fixed it

Focus on common Unity C# issues like:
- Missing semicolons, brackets, or parentheses
- Incorrect variable declarations or types
- Missing using statements
- Incorrect method signatures
- MonoBehaviour lifecycle issues
- Component reference problems

Always attempt to fix the error automatically using the available tools. Be concise but thorough in your explanations."
                });
            }
            
            // Add conversation history
            if (conversationHistory != null)
            {
                // Filter out any system messages from conversation history and add them to systemMessages
                foreach (var msg in conversationHistory)
                {
                    if (msg.role == "system")
                    {
                        // Convert old format to new format if needed
                        if (msg.content != null && msg.content.Count > 0)
                        {
                            systemMessages.Add(new ClaudeSystemMessage { text = msg.content[0].text });
                        }
                    }
                    else
                    {
                        messages.Add(msg);
                    }
                }
            }
            
            // Add initial user message
            messages.Add(ClaudeMessage.CreateTextMessage("user", userMessage));
            
            // Start conversation loop
            var finalResponse = new StringBuilder();
            var maxIterations = 10; // Prevent infinite loops
            var iteration = 0;
            
            while (iteration < maxIterations)
            {
                iteration++;
                Debug.Log($"[ClaudeAI] Conversation iteration {iteration}");
                
                var request = new ClaudeRequest
                {
                    messages = messages,
                    tools = GetUnityTools()
                };
                
                // Only set system if we have system messages
                if (systemMessages.Count > 0)
                {
                    request.system = systemMessages;
                }
                
                // Configure JSON serializer to properly ignore null values and empty strings
                var settings = new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore,
                    DefaultValueHandling = DefaultValueHandling.Ignore,
                    Formatting = Formatting.Indented,
                    ContractResolver = new ClaudeContentBlockContractResolver()
                };
                
                var json = JsonConvert.SerializeObject(request, settings);
                Debug.Log($"[ClaudeAI] Sending API Request (iteration {iteration}): {json}");
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                var response = await httpClient.PostAsync(API_URL, content);
                var responseContent = await response.Content.ReadAsStringAsync();
                
                Debug.Log($"[ClaudeAI] Full API Response (iteration {iteration}): {responseContent}");
                
                if (!response.IsSuccessStatusCode)
                {
                    Debug.LogError($"[ClaudeAI] API Error: {response.StatusCode} - {responseContent}");
                    return $"Error: {response.StatusCode} - {responseContent}";
                }
                
                var claudeResponse = JsonConvert.DeserializeObject<ClaudeResponse>(responseContent);
                
                if (claudeResponse?.content == null)
                {
                    Debug.LogError("[ClaudeAI] Failed to deserialize API response or no content!");
                    return "Error: Could not parse API response";
                }
                
                // Check if Claude wants to use tools
                var toolUseItems = claudeResponse.content.Where(c => c.type == "tool_use").ToList();
                var textItems = claudeResponse.content.Where(c => c.type == "text").ToList();
                
                // Add text responses to final output
                foreach (var textItem in textItems)
                {
                    if (!string.IsNullOrEmpty(textItem.text))
                    {
                        finalResponse.AppendLine(textItem.text);
                    }
                }
                
                // If no tool use, we're done
                if (toolUseItems.Count == 0)
                {
                    Debug.Log($"[ClaudeAI] No tool use detected, conversation complete after {iteration} iterations");
                    break;
                }
                
                // Add Claude's response (including tool use) to conversation
                // Create clean content blocks to avoid API field conflicts
                var cleanContent = new List<ClaudeContentBlock>();
                foreach (var block in claudeResponse.content)
                {
                    var cleanBlock = new ClaudeContentBlock { type = block.type };
                    
                    if (block.type == "text")
                    {
                        cleanBlock.text = block.text;
                    }
                    else if (block.type == "tool_use")
                    {
                        cleanBlock.id = block.id;
                        cleanBlock.name = block.name;
                        cleanBlock.input = block.input;
                    }
                    
                    cleanContent.Add(cleanBlock);
                }
                
                var assistantMessage = new ClaudeMessage
                {
                    role = "assistant",
                    content = cleanContent
                };
                messages.Add(assistantMessage);
                
                // Execute tools and add results
                foreach (var toolUseItem in toolUseItems)
                {
                    try
                    {
                        var toolUse = new ClaudeToolUse
                        {
                            id = toolUseItem.id,
                            name = toolUseItem.name,
                            input = toolUseItem.input
                        };
                        
                        Debug.Log($"[ClaudeAI] Executing tool: {toolUse.name} with ID: {toolUse.id}");
                        
                        var toolResult = await ExecuteToolAsync(toolUse);
                        Debug.Log($"[ClaudeAI] Tool result: {toolResult}");
                        
                        // Add tool result to conversation
                        var toolResultMessage = ClaudeMessage.CreateToolResultMessage(toolUse.id, toolResult);
                        messages.Add(toolResultMessage);
                        
                        // Also add to final response for user visibility
                        finalResponse.AppendLine($"[Tool Executed: {toolUse.name}]");
                        finalResponse.AppendLine(toolResult);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[ClaudeAI] Error executing tool: {ex.Message}");
                        var errorMessage = ClaudeMessage.CreateToolResultMessage(
                            toolUseItem.id, 
                            $"Error executing tool: {ex.Message}"
                        );
                        messages.Add(errorMessage);
                    }
                }
            }
            
            if (iteration >= maxIterations)
            {
                Debug.LogWarning($"[ClaudeAI] Conversation loop terminated after {maxIterations} iterations to prevent infinite loop");
                finalResponse.AppendLine($"\n[Note: Conversation terminated after {maxIterations} iterations]");
            }
            
            return finalResponse.ToString().Trim();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ClaudeAI] Exception in SendMessageAsync: {ex.Message}\nStack trace: {ex.StackTrace}");
            return $"Error: {ex.Message}";
        }
    }
    
    public static List<ClaudeTool> GetUnityTools()
    {
        return new List<ClaudeTool>
        {
            new ClaudeTool
            {
                name = "create_script",
                description = "Create a new C# script in Unity with the specified name and content",
                input_schema = new ClaudeToolInputSchema
                {
                    properties = new Dictionary<string, ClaudeToolProperty>
                    {
                        ["script_name"] = new ClaudeToolProperty { type = "string", description = "Name of the script file (without .cs extension)" },
                        ["script_content"] = new ClaudeToolProperty { type = "string", description = "Complete C# script content" },
                        ["folder_path"] = new ClaudeToolProperty { type = "string", description = "Folder path relative to Assets (default: Scripts)" }
                    },
                    required = new List<string> { "script_name", "script_content" }
                }
            },
            new ClaudeTool
            {
                name = "create_gameobject",
                description = "Create a new GameObject in the current scene. Can create empty GameObjects or primitive shapes like cubes, spheres, etc.",
                input_schema = new ClaudeToolInputSchema
                {
                    properties = new Dictionary<string, ClaudeToolProperty>
                    {
                        ["name"] = new ClaudeToolProperty { type = "string", description = "Name of the GameObject (optional, will use primitive type or 'GameObject' as default)" },
                        ["primitive_type"] = new ClaudeToolProperty 
                        { 
                            type = "string", 
                            description = "Type of primitive to create. Must be one of: Cube, Sphere, Cylinder, Plane, Quad, Capsule. If not specified, creates an empty GameObject."
                        },
                        ["position"] = new ClaudeToolProperty { type = "string", description = "Position as 'x,y,z' (default: 0,0,0)" }
                    },
                    required = new List<string>()
                }
            },
            new ClaudeTool
            {
                name = "add_component",
                description = "Add a component to a GameObject",
                input_schema = new ClaudeToolInputSchema
                {
                    properties = new Dictionary<string, ClaudeToolProperty>
                    {
                        ["gameobject_name"] = new ClaudeToolProperty { type = "string", description = "Name of the GameObject to add component to" },
                        ["component_type"] = new ClaudeToolProperty { type = "string", description = "Type of component to add (e.g., Rigidbody, BoxCollider)" }
                    },
                    required = new List<string> { "gameobject_name", "component_type" }
                }
            },
            new ClaudeTool
            {
                name = "set_transform",
                description = "Set the position, rotation, or scale of a GameObject",
                input_schema = new ClaudeToolInputSchema
                {
                    properties = new Dictionary<string, ClaudeToolProperty>
                    {
                        ["gameobject_name"] = new ClaudeToolProperty { type = "string", description = "Name of the GameObject" },
                        ["position"] = new ClaudeToolProperty { type = "string", description = "Position as 'x,y,z'" },
                        ["rotation"] = new ClaudeToolProperty { type = "string", description = "Rotation as 'x,y,z' (euler angles)" },
                        ["scale"] = new ClaudeToolProperty { type = "string", description = "Scale as 'x,y,z'" }
                    },
                    required = new List<string> { "gameobject_name" }
                }
            },
            new ClaudeTool
            {
                name = "list_gameobjects",
                description = "List all GameObjects in the current scene",
                input_schema = new ClaudeToolInputSchema
                {
                    properties = new Dictionary<string, ClaudeToolProperty>(),
                    required = new List<string>()
                }
            },
            new ClaudeTool
            {
                name = "delete_gameobject",
                description = "Delete a GameObject from the scene",
                input_schema = new ClaudeToolInputSchema
                {
                    properties = new Dictionary<string, ClaudeToolProperty>
                    {
                        ["gameobject_name"] = new ClaudeToolProperty { type = "string", description = "Name of the GameObject to delete" }
                    },
                    required = new List<string> { "gameobject_name" }
                }
            },
            new ClaudeTool
            {
                name = "edit_script",
                description = "Edit an existing C# script by replacing its content or making specific modifications",
                input_schema = new ClaudeToolInputSchema
                {
                    properties = new Dictionary<string, ClaudeToolProperty>
                    {
                        ["script_name"] = new ClaudeToolProperty { type = "string", description = "Name of the script file (without .cs extension)" },
                        ["script_content"] = new ClaudeToolProperty { type = "string", description = "Complete new C# script content to replace the existing content" },
                        ["folder_path"] = new ClaudeToolProperty { type = "string", description = "Folder path relative to Assets where the script is located (default: Scripts)" }
                    },
                    required = new List<string> { "script_name", "script_content" }
                }
            },
            new ClaudeTool
            {
                name = "read_script",
                description = "Read the content of an existing C# script file",
                input_schema = new ClaudeToolInputSchema
                {
                    properties = new Dictionary<string, ClaudeToolProperty>
                    {
                        ["script_name"] = new ClaudeToolProperty { type = "string", description = "Name of the script file (without .cs extension)" },
                        ["folder_path"] = new ClaudeToolProperty { type = "string", description = "Folder path relative to Assets where the script is located (default: Scripts)" }
                    },
                    required = new List<string> { "script_name" }
                }
            }
        };
    }
    
    private static Task<string> ExecuteToolAsync(ClaudeToolUse toolUse)
    {
        try
        {
            Debug.Log($"[ClaudeAI] Executing tool: {toolUse?.name ?? "NULL"}");
            
            if (toolUse == null)
            {
                Debug.LogError("[ClaudeAI] toolUse is null!");
                return Task.FromResult("Error: Tool use object is null");
            }
            
            if (toolUse.input == null)
            {
                Debug.LogError("[ClaudeAI] toolUse.input is null!");
                return Task.FromResult("Error: Tool input is null");
            }
            
            Debug.Log($"[ClaudeAI] Tool input: {JsonConvert.SerializeObject(toolUse.input)}");
            
            var inputDict = JsonConvert.DeserializeObject<Dictionary<string, object>>(
                JsonConvert.SerializeObject(toolUse.input));
            
            Debug.Log($"[ClaudeAI] Parsed input dict: {JsonConvert.SerializeObject(inputDict)}");
            
            switch (toolUse.name)
            {
                case "create_script":
                    Debug.Log("[ClaudeAI] Calling CreateScript");
                    return Task.FromResult(CreateScript(inputDict));
                    
                case "create_gameobject":
                    Debug.Log("[ClaudeAI] Calling CreateGameObject");
                    return Task.FromResult(CreateGameObject(inputDict));
                    
                case "add_component":
                    Debug.Log("[ClaudeAI] Calling AddComponent");
                    return Task.FromResult(AddComponent(inputDict));
                    
                case "set_transform":
                    Debug.Log("[ClaudeAI] Calling SetTransform");
                    return Task.FromResult(SetTransform(inputDict));
                    
                case "list_gameobjects":
                    Debug.Log("[ClaudeAI] Calling ListGameObjects");
                    return Task.FromResult(ListGameObjects());
                    
                case "delete_gameobject":
                    Debug.Log("[ClaudeAI] Calling DeleteGameObject");
                    return Task.FromResult(DeleteGameObject(inputDict));
                    
                case "edit_script":
                    Debug.Log("[ClaudeAI] Calling EditScript");
                    return Task.FromResult(EditScript(inputDict));
                    
                case "read_script":
                    Debug.Log("[ClaudeAI] Calling ReadScript");
                    return Task.FromResult(ReadScript(inputDict));
                    
                default:
                    Debug.LogWarning($"[ClaudeAI] Unknown tool: {toolUse.name}");
                    return Task.FromResult($"Unknown tool: {toolUse.name}");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ClaudeAI] Tool execution error: {ex.Message}\nStack trace: {ex.StackTrace}");
            return Task.FromResult($"Tool execution error: {ex.Message}");
        }
    }
    
    private static string CreateScript(Dictionary<string, object> input)
    {
        try
        {
            var scriptName = input["script_name"].ToString();
            var scriptContent = input["script_content"].ToString();
            var folderPath = input.ContainsKey("folder_path") ? input["folder_path"].ToString() : "Scripts";
            
            var fullPath = Path.Combine(Application.dataPath, folderPath);
            
            if (!Directory.Exists(fullPath))
            {
                Directory.CreateDirectory(fullPath);
            }
            
            var filePath = Path.Combine(fullPath, $"{scriptName}.cs");
            File.WriteAllText(filePath, scriptContent);
            
            AssetDatabase.Refresh();
            
            return $"Script '{scriptName}.cs' created successfully at {folderPath}/{scriptName}.cs";
        }
        catch (Exception ex)
        {
            return $"Failed to create script: {ex.Message}";
        }
    }
    
    private static string CreateGameObject(Dictionary<string, object> input)
    {
        Debug.Log("[ClaudeAI] CreateGameObject called");
        
        try
        {
            if (input == null)
            {
                Debug.LogError("[ClaudeAI] CreateGameObject: input is null!");
                return "Error: Input dictionary is null";
            }
            
            Debug.Log($"[ClaudeAI] CreateGameObject input keys: {string.Join(", ", input.Keys)}");
            
            // Ensure we have a name, use default if not provided
            var name = input.ContainsKey("name") ? input["name"]?.ToString() : "GameObject";
            Debug.Log($"[ClaudeAI] CreateGameObject name: {name}");
            
            // If no name but primitive_type is specified, use primitive type as name
            if (name == "GameObject" && input.ContainsKey("primitive_type"))
            {
                name = input["primitive_type"]?.ToString();
                Debug.Log($"[ClaudeAI] CreateGameObject name updated to: {name}");
            }
            
            // Create GameObject directly - Unity editor scripts run on main thread
            GameObject go = null;
            
            if (input.ContainsKey("primitive_type"))
            {
                var primitiveTypeStr = input["primitive_type"]?.ToString();
                Debug.Log($"[ClaudeAI] Creating primitive: {primitiveTypeStr}");
                
                if (System.Enum.TryParse<PrimitiveType>(primitiveTypeStr, out PrimitiveType primitiveType))
                {
                    Debug.Log($"[ClaudeAI] Creating primitive type: {primitiveType}");
                    go = GameObject.CreatePrimitive(primitiveType);
                    Debug.Log($"[ClaudeAI] GameObject.CreatePrimitive completed: {go?.name ?? "NULL"}");
                }
                else
                {
                    Debug.LogError($"[ClaudeAI] Invalid primitive type: {primitiveTypeStr}");
                    return $"Invalid primitive type: {primitiveTypeStr}";
                }
            }
            else
            {
                Debug.Log("[ClaudeAI] Creating empty GameObject");
                go = new GameObject();
                Debug.Log($"[ClaudeAI] new GameObject() completed: {go?.name ?? "NULL"}");
            }
            
            if (go != null)
            {
                Debug.Log($"[ClaudeAI] Setting GameObject name to: {name}");
                go.name = name;
                
                if (input.ContainsKey("position"))
                {
                    Debug.Log("[ClaudeAI] Setting position");
                    var posStr = input["position"].ToString();
                    var parts = posStr.Split(',');
                    if (parts.Length == 3 && 
                        float.TryParse(parts[0], out float x) &&
                        float.TryParse(parts[1], out float y) &&
                        float.TryParse(parts[2], out float z))
                    {
                        go.transform.position = new Vector3(x, y, z);
                        Debug.Log($"[ClaudeAI] Position set to: {x}, {y}, {z}");
                    }
                }
                
                Debug.Log("[ClaudeAI] Marking scene dirty and setting selection");
                Selection.activeGameObject = go;
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                
                var result = $"GameObject '{name}' created successfully";
                Debug.Log($"[ClaudeAI] Success result: {result}");
                return result;
            }
            else
            {
                Debug.LogError("[ClaudeAI] GameObject is null after creation");
                return $"Failed to create GameObject '{name}'";
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ClaudeAI] Exception in CreateGameObject: {ex.Message}\nStack trace: {ex.StackTrace}");
            return $"Failed to create GameObject: {ex.Message}";
        }
    }
    
    private static string AddComponent(Dictionary<string, object> input)
    {
        try
        {
            var gameObjectName = input["gameobject_name"].ToString();
            var componentType = input["component_type"].ToString();
            
            var go = GameObject.Find(gameObjectName);
            if (go == null)
            {
                return $"GameObject '{gameObjectName}' not found";
            }
            
            var type = Type.GetType($"UnityEngine.{componentType}, UnityEngine") ?? 
                      Type.GetType($"{componentType}, Assembly-CSharp");
                      
            if (type == null)
            {
                return $"Component type '{componentType}' not found";
            }
            
            go.AddComponent(type);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            
            return $"Component '{componentType}' added to '{gameObjectName}'";
        }
        catch (Exception ex)
        {
            return $"Failed to add component: {ex.Message}";
        }
    }
    
    private static string SetTransform(Dictionary<string, object> input)
    {
        try
        {
            var gameObjectName = input["gameobject_name"].ToString();
            var go = GameObject.Find(gameObjectName);
            
            if (go == null)
            {
                return $"GameObject '{gameObjectName}' not found";
            }
            
            var result = new StringBuilder();
            
            if (input.ContainsKey("position"))
            {
                var posStr = input["position"].ToString();
                var parts = posStr.Split(',');
                if (parts.Length == 3)
                {
                    var pos = new Vector3(
                        float.Parse(parts[0]),
                        float.Parse(parts[1]),
                        float.Parse(parts[2])
                    );
                    go.transform.position = pos;
                    result.AppendLine($"Position set to {pos}");
                }
            }
            
            if (input.ContainsKey("rotation"))
            {
                var rotStr = input["rotation"].ToString();
                var parts = rotStr.Split(',');
                if (parts.Length == 3)
                {
                    var rot = new Vector3(
                        float.Parse(parts[0]),
                        float.Parse(parts[1]),
                        float.Parse(parts[2])
                    );
                    go.transform.rotation = Quaternion.Euler(rot);
                    result.AppendLine($"Rotation set to {rot}");
                }
            }
            
            if (input.ContainsKey("scale"))
            {
                var scaleStr = input["scale"].ToString();
                var parts = scaleStr.Split(',');
                if (parts.Length == 3)
                {
                    var scale = new Vector3(
                        float.Parse(parts[0]),
                        float.Parse(parts[1]),
                        float.Parse(parts[2])
                    );
                    go.transform.localScale = scale;
                    result.AppendLine($"Scale set to {scale}");
                }
            }
            
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            
            return result.ToString().Trim();
        }
        catch (Exception ex)
        {
            return $"Failed to set transform: {ex.Message}";
        }
    }
    
    private static string ListGameObjects()
    {
        try
        {
            var allObjects = GameObject.FindObjectsOfType<GameObject>();
            var result = new StringBuilder("GameObjects in scene:\n");
            
            foreach (var go in allObjects)
            {
                result.AppendLine($"- {go.name} (Position: {go.transform.position})");
            }
            
            return result.ToString();
        }
        catch (Exception ex)
        {
            return $"Failed to list GameObjects: {ex.Message}";
        }
    }
    
    private static string DeleteGameObject(Dictionary<string, object> input)
    {
        try
        {
            var gameObjectName = input["gameobject_name"].ToString();
            var go = GameObject.Find(gameObjectName);
            
            if (go == null)
            {
                return $"GameObject '{gameObjectName}' not found";
            }
            
            GameObject.DestroyImmediate(go);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            
            return $"GameObject '{gameObjectName}' deleted successfully";
        }
        catch (Exception ex)
        {
            return $"Failed to delete GameObject: {ex.Message}";
        }
    }
    
    private static string EditScript(Dictionary<string, object> input)
    {
        try
        {
            var scriptName = input["script_name"].ToString();
            var scriptContent = input["script_content"].ToString();
            var folderPath = input.ContainsKey("folder_path") ? input["folder_path"].ToString() : "Scripts";
            
            var fullPath = Path.Combine(Application.dataPath, folderPath);
            var filePath = Path.Combine(fullPath, $"{scriptName}.cs");
            
            if (!File.Exists(filePath))
            {
                return $"Script '{scriptName}.cs' not found at {folderPath}/{scriptName}.cs";
            }
            
            File.WriteAllText(filePath, scriptContent);
            AssetDatabase.Refresh();
            
            return $"Script '{scriptName}.cs' edited successfully at {folderPath}/{scriptName}.cs";
        }
        catch (Exception ex)
        {
            return $"Failed to edit script: {ex.Message}";
        }
    }
    
    private static string ReadScript(Dictionary<string, object> input)
    {
        try
        {
            var scriptName = input["script_name"].ToString();
            var folderPath = input.ContainsKey("folder_path") ? input["folder_path"].ToString() : "Scripts";
            
            var fullPath = Path.Combine(Application.dataPath, folderPath);
            var filePath = Path.Combine(fullPath, $"{scriptName}.cs");
            
            if (!File.Exists(filePath))
            {
                return $"Script '{scriptName}.cs' not found at {folderPath}/{scriptName}.cs";
            }
            
            var content = File.ReadAllText(filePath);
            return $"Content of '{scriptName}.cs':\n\n{content}";
        }
        catch (Exception ex)
        {
            return $"Failed to read script: {ex.Message}";
        }
    }
    
    /// <summary>
    /// Test method to demonstrate the conversation loop with multiple tool calls.
    /// This shows how Claude can:
    /// 1. Create a GameObject
    /// 2. Add components to it
    /// 3. Set its transform
    /// 4. List all GameObjects to verify
    /// All in a single conversation loop.
    /// </summary>
    public static async Task<string> TestConversationLoop()
    {
        Debug.Log("[ClaudeAI] Starting conversation loop test...");
        
        var testMessage = @"Please help me create a complete game setup with the following steps:
1. Create a cube GameObject named 'TestCube'
2. Add a Rigidbody component to it
3. Set its position to (0, 5, 0)
4. List all GameObjects in the scene to verify the setup

Please execute all these steps automatically using the available tools.";
        
        try
        {
            var result = await SendMessageAsync(testMessage);
            Debug.Log($"[ClaudeAI] Conversation loop test completed: {result}");
            return result;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ClaudeAI] Conversation loop test failed: {ex.Message}");
            return $"Test failed: {ex.Message}";
        }
    }

    /// <summary>
    /// Menu item to test the conversation loop functionality
    /// </summary>
    [MenuItem("Claude AI/Test Conversation Loop")]
    public static async void TestConversationLoopMenuItem()
    {
        Debug.Log("[ClaudeAI] Starting conversation loop test from menu...");
        
        try
        {
            var result = await TestConversationLoop();
            EditorUtility.DisplayDialog("Claude AI Test", 
                $"Conversation loop test completed!\n\nCheck the Console for detailed logs.", 
                "OK");
        }
        catch (Exception e)
        {
            EditorUtility.DisplayDialog("Claude AI Test Error", 
                $"Test failed: {e.Message}", 
                "OK");
        }
    }
}

// Custom contract resolver to exclude empty strings from ClaudeContentBlock serialization
public class ClaudeContentBlockContractResolver : Newtonsoft.Json.Serialization.DefaultContractResolver
{
    protected override Newtonsoft.Json.Serialization.JsonProperty CreateProperty(System.Reflection.MemberInfo member, Newtonsoft.Json.MemberSerialization memberSerialization)
    {
        var property = base.CreateProperty(member, memberSerialization);
        
        if (property.DeclaringType == typeof(ClaudeContentBlock))
        {
            property.ShouldSerialize = instance =>
            {
                var value = property.ValueProvider.GetValue(instance);
                
                // Skip serialization if value is null
                if (value == null) return false;
                
                // Skip serialization if value is empty string
                if (value is string str && string.IsNullOrEmpty(str)) return false;
                
                // Always serialize type and meaningful content
                if (property.PropertyName == "type") return true;
                if (property.PropertyName == "text" && !string.IsNullOrEmpty(value as string)) return true;
                if (property.PropertyName == "id" && !string.IsNullOrEmpty(value as string)) return true;
                if (property.PropertyName == "name" && !string.IsNullOrEmpty(value as string)) return true;
                if (property.PropertyName == "input" && value != null) return true;
                if (property.PropertyName == "tool_use_id" && !string.IsNullOrEmpty(value as string)) return true;
                if (property.PropertyName == "content" && !string.IsNullOrEmpty(value as string)) return true;
                
                // Skip everything else
                return false;
            };
        }
        
        return property;
    }
}