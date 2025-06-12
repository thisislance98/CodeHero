using UnityEngine;
using System;
using System.Collections.Generic;

[System.Serializable]
public class ChatMessage
{
    public string id;
    public string username;
    public string message;
    public string timestamp;
    public MessageType type;
    
    public ChatMessage(string user, string msg, MessageType msgType = MessageType.Normal)
    {
        id = System.Guid.NewGuid().ToString();
        username = user;
        message = msg;
        timestamp = DateTime.Now.ToString("HH:mm:ss");
        type = msgType;
    }
}

public enum MessageType
{
    Normal,
    System,
    Warning,
    Error
}

[System.Serializable]
public class LogEntry
{
    public string timestamp;
    public string logString;
    public string stackTrace;
    public LogType type;
    
    public LogEntry(string log, string stack, LogType logType)
    {
        timestamp = DateTime.Now.ToString("HH:mm:ss");
        logString = log;
        stackTrace = stack;
        type = logType;
    }
}

 