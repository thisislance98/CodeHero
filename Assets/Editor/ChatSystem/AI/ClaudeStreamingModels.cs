using Newtonsoft.Json;

[System.Serializable]
public class StreamEvent
{
    public string type;
}

[System.Serializable]
public class ContentBlockStartEvent : StreamEvent
{
    public int index;
    public ClaudeContentBlock content_block;
}

[System.Serializable]
public class ContentBlockDeltaEvent : StreamEvent
{
    public int index;
    public StreamDelta delta;
}

[System.Serializable]
public class MessageDeltaEvent : StreamEvent
{
    public MessageDelta delta;
}

[System.Serializable]
public class StreamErrorEvent : StreamEvent
{
    public StreamError error;
}

[System.Serializable]
public class StreamDelta
{
    public string type;
    
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string text;
    
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string partial_json;
}

[System.Serializable]
public class MessageDelta
{
    public string stop_reason;
}

[System.Serializable]
public class StreamError
{
    public string type;
    public string message;
} 