using UnityEngine;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using Newtonsoft.Json;

// Unified tool coordinator that delegates to specialized tool classes
public static class UnityTools
{
    public static List<ClaudeTool> GetUnityTools()
    {
        var allTools = new List<ClaudeTool>();
        
        // Combine tools from all specialized tool classes
        allTools.AddRange(ScriptTools.GetScriptTools());
        allTools.AddRange(GameObjectTools.GetGameObjectTools());
        allTools.AddRange(FileSystemTools.GetFileSystemTools());
        allTools.AddRange(TextEditorTools.GetTextEditorTools());
        
        return allTools;
    }
    
    public static async Task<string> ExecuteToolAsync(ClaudeToolUse toolUse)
    {
        try
        {
            Debug.Log($"[ClaudeAI] Executing tool: {toolUse?.name ?? "NULL"}");
            
            if (toolUse == null)
            {
                Debug.LogError("[ClaudeAI] toolUse is null!");
                return "Error: Tool use object is null";
            }
            
            // Handle null input by providing empty object for tools that don't require parameters
            if (toolUse.input == null)
            {
                Debug.Log($"[ClaudeAI] toolUse.input is null, providing empty object for tool: {toolUse.name}");
                toolUse.input = new object(); // Provide empty object as default
            }
            
            Debug.Log($"[ClaudeAI] Tool input: {JsonConvert.SerializeObject(toolUse.input)}");
            
            // Delegate to appropriate tool class based on tool name
            switch (toolUse.name)
            {
                // Script tools
                case "create_script":
                    Debug.Log("[ClaudeAI] Delegating to ScriptTools");
                    return await ScriptTools.ExecuteScriptToolAsync(toolUse);
                    
                // GameObject tools
                case "create_gameobject":
                case "add_component":
                case "set_transform":
                case "list_gameobjects":
                case "delete_gameobject":
                case "view_gameobject":
                    Debug.Log("[ClaudeAI] Delegating to GameObjectTools");
                    return GameObjectTools.ExecuteGameObjectTool(toolUse);
                    
                // File system tools
                case "search_files":
                    Debug.Log("[ClaudeAI] Delegating to FileSystemTools");
                    return FileSystemTools.ExecuteFileSystemTool(toolUse);
                    
                // Text editor tools
                case "str_replace_based_edit_tool":
                    Debug.Log("[ClaudeAI] Delegating to TextEditorTools");
                    return TextEditorTools.ExecuteTextEditorTool(toolUse);
                    
                default:
                    Debug.LogWarning($"[ClaudeAI] Unknown tool: {toolUse.name}");
                    return $"Unknown tool: {toolUse.name}";
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ClaudeAI] Tool execution error: {ex.Message}\nStack trace: {ex.StackTrace}");
            return $"Tool execution error: {ex.Message}";
        }
    }
} 