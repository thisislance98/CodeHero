using UnityEngine;
using System.Collections.Generic;
using System;
using Newtonsoft.Json;

// Unified tool coordinator that delegates to specialized tool classes
public static class UnityTools
{
    // Tool execution deduplication system
    private static HashSet<string> executingTools = new HashSet<string>();
    private static readonly object toolLock = new object();
    
    public static List<ClaudeTool> GetUnityTools()
    {
        var allTools = new List<ClaudeTool>();
        
        // Combine tools from all specialized tool classes
        allTools.AddRange(ScriptTools.GetScriptTools());
        allTools.AddRange(GameObjectTools.GetGameObjectTools());
        allTools.AddRange(FileSystemTools.GetFileSystemTools());
        allTools.AddRange(TextEditorTools.GetTextEditorTools());
        allTools.AddRange(UnityLogTools.GetUnityLogTools());
        
        // New tool classes from MCP server integration
        allTools.AddRange(AssetTools.GetAssetTools());
        allTools.AddRange(SceneTools.GetSceneTools());
        allTools.AddRange(EditorTools.GetEditorTools());
        
        // High priority new tools
        allTools.AddRange(TerminalTools.GetTerminalTools());
        allTools.AddRange(CodebaseSearchTools.GetCodebaseSearchTools());
        allTools.AddRange(AdvancedFileTools.GetAdvancedFileTools());
        allTools.AddRange(DirectoryTools.GetDirectoryTools());
        
        return allTools;
    }
    
    public static string ExecuteTool(ClaudeToolUse toolUse)
    {
        try
        {
            Debug.Log($"[ClaudeAI] Executing tool: {toolUse?.name ?? "NULL"}");
            
            // Send debug message to chat window that persists
            ChatWindow.SendDebugMessage($"Tool execution started: {toolUse?.name ?? "NULL"}");
            
            if (toolUse == null)
            {
                Debug.LogError("[ClaudeAI] toolUse is null!");
                ChatWindow.SendDebugMessage("ERROR: toolUse is null!");
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
                    ChatWindow.SendDebugMessage("Delegating create_script to ScriptTools");
                    var scriptResult = ScriptTools.ExecuteScriptTool(toolUse);
                    ChatWindow.SendDebugMessage($"ScriptTools returned: {scriptResult?.Substring(0, Math.Min(100, scriptResult?.Length ?? 0))}...");
                    return scriptResult;
                    
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
                    
                // Unity log tools
                case "read_unity_logs":
                case "check_compilation_status":
                case "clear_console":
                    Debug.Log("[ClaudeAI] Delegating to UnityLogTools");
                    return UnityLogTools.ExecuteUnityLogTool(toolUse);
                    
                // Asset tools
                case "manage_asset":
                case "refresh_assets":
                    Debug.Log("[ClaudeAI] Delegating to AssetTools");
                    return AssetTools.ExecuteAssetTool(toolUse);
                    
                // Scene tools
                case "manage_scene":
                    Debug.Log("[ClaudeAI] Delegating to SceneTools");
                    return SceneTools.ExecuteSceneTool(toolUse);
                    
                // Editor tools
                case "manage_editor":
                case "execute_menu_item":
                    Debug.Log("[ClaudeAI] Delegating to EditorTools");
                    return EditorTools.ExecuteEditorTool(toolUse);
                    
                // Terminal tools
                case "execute_terminal_command":
                    Debug.Log("[ClaudeAI] Delegating to TerminalTools");
                    return TerminalTools.ExecuteTerminalTool(toolUse);
                    
                // Codebase search tools
                case "search_codebase":
                case "find_references":
                    Debug.Log("[ClaudeAI] Delegating to CodebaseSearchTools");
                    return CodebaseSearchTools.ExecuteCodebaseSearchTool(toolUse);
                    
                // Advanced file tools
                case "delete_file":
                case "move_file":
                case "copy_file":
                    Debug.Log("[ClaudeAI] Delegating to AdvancedFileTools");
                    return AdvancedFileTools.ExecuteAdvancedFileTool(toolUse);
                    
                // Directory tools
                case "list_directory":
                case "create_directory":
                case "analyze_directory":
                    Debug.Log("[ClaudeAI] Delegating to DirectoryTools");
                    return DirectoryTools.ExecuteDirectoryTool(toolUse);
                    
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