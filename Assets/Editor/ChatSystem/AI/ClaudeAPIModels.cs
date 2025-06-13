using System.Collections.Generic;
using Newtonsoft.Json;

[System.Serializable]
public class ClaudeMessage
{
    public string role;
    public List<ClaudeContentBlock> content;
    
    public ClaudeMessage()
    {
        content = new List<ClaudeContentBlock>();
    }
    
    public static ClaudeMessage CreateTextMessage(string role, string text)
    {
        var contentBlock = new ClaudeContentBlock();
        contentBlock.type = "text";
        contentBlock.text = text;
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
    
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string id = null;
    
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string name = null;
    
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public object input = null;
    
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string tool_use_id = null;
    
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string content = null;
    
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string partial_input = null;
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
    public int max_tokens = 8192;
    
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public List<ClaudeSystemMessage> system;
    
    public List<ClaudeMessage> messages;
    public List<ClaudeTool> tools;
    
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public bool? stream;
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
    
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string description;
    
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public ClaudeToolInputSchema input_schema;
    
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string type;
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