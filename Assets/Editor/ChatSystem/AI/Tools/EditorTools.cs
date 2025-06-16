using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;
using System.Text;
using System;
using Newtonsoft.Json;
using System.Linq;
using System.Reflection;

public static class EditorTools
{
    public static List<ClaudeTool> GetEditorTools()
    {
        return new List<ClaudeTool>
        {
            new ClaudeTool
            {
                name = "manage_editor",
                description = "Control Unity editor state and settings including play mode, pause, active tool selection, and project settings.",
                input_schema = new ClaudeToolInputSchema
                {
                    properties = new Dictionary<string, ClaudeToolProperty>
                    {
                        ["action"] = new ClaudeToolProperty { type = "string", description = "Operation to perform: 'play', 'pause', 'stop', 'get_state', 'set_active_tool', 'add_tag', 'add_layer', 'get_tags', 'get_layers', 'get_project_settings'" },
                        ["wait_for_completion"] = new ClaudeToolProperty { type = "boolean", description = "Wait for certain operations to complete (default: false)" },
                        ["tool_name"] = new ClaudeToolProperty { type = "string", description = "Name of the tool to activate (e.g., 'MoveTool', 'RotateTool', 'ScaleTool')" },
                        ["tag_name"] = new ClaudeToolProperty { type = "string", description = "Name of the tag to add" },
                        ["layer_name"] = new ClaudeToolProperty { type = "string", description = "Name of the layer to add" }
                    },
                    required = new List<string> { "action" }
                }
            },
            new ClaudeTool
            {
                name = "execute_menu_item",
                description = "Execute Unity Editor menu items by their menu path (e.g., 'File/Save Project', 'Assets/Refresh').",
                input_schema = new ClaudeToolInputSchema
                {
                    properties = new Dictionary<string, ClaudeToolProperty>
                    {
                        ["menu_path"] = new ClaudeToolProperty { type = "string", description = "Full path of the menu item to execute (e.g., 'File/Save Project', 'Assets/Refresh', 'GameObject/Create Empty')" },
                        ["validate_only"] = new ClaudeToolProperty { type = "boolean", description = "Only validate if menu item exists without executing (default: false)" }
                    },
                    required = new List<string> { "menu_path" }
                }
            }
        };
    }
    
    public static string ExecuteEditorTool(ClaudeToolUse toolUse)
    {
        var inputDict = JsonConvert.DeserializeObject<Dictionary<string, object>>(
            JsonConvert.SerializeObject(toolUse.input));
        
        switch (toolUse.name)
        {
            case "manage_editor":
                return ManageEditor(inputDict);
            case "execute_menu_item":
                return ExecuteMenuItem(inputDict);
            default:
                return $"Unknown editor tool: {toolUse.name}";
        }
    }
    
    private static string ManageEditor(Dictionary<string, object> input)
    {
        try
        {
            var action = input["action"].ToString().ToLower();
            
            switch (action)
            {
                case "play":
                    return PlayEditor();
                case "pause":
                    return PauseEditor();
                case "stop":
                    return StopEditor();
                case "get_state":
                    return GetEditorState();
                case "set_active_tool":
                    return SetActiveTool(input);
                case "add_tag":
                    return AddTag(input);
                case "add_layer":
                    return AddLayer(input);
                case "get_tags":
                    return GetTags();
                case "get_layers":
                    return GetLayers();
                case "get_project_settings":
                    return GetProjectSettings();
                default:
                    return $"Unknown editor action: {action}";
            }
        }
        catch (Exception ex)
        {
            return $"Editor operation failed: {ex.Message}";
        }
    }
    
    private static string PlayEditor()
    {
        try
        {
            if (EditorApplication.isPlaying)
            {
                return "Editor is already playing";
            }
            
            EditorApplication.isPlaying = true;
            return "Started play mode";
        }
        catch (Exception ex)
        {
            return $"Failed to start play mode: {ex.Message}";
        }
    }
    
    private static string PauseEditor()
    {
        try
        {
            if (!EditorApplication.isPlaying)
            {
                return "Editor is not playing - cannot pause";
            }
            
            EditorApplication.isPaused = !EditorApplication.isPaused;
            return EditorApplication.isPaused ? "Paused play mode" : "Resumed play mode";
        }
        catch (Exception ex)
        {
            return $"Failed to pause/resume play mode: {ex.Message}";
        }
    }
    
    private static string StopEditor()
    {
        try
        {
            if (!EditorApplication.isPlaying)
            {
                return "Editor is not playing";
            }
            
            EditorApplication.isPlaying = false;
            return "Stopped play mode";
        }
        catch (Exception ex)
        {
            return $"Failed to stop play mode: {ex.Message}";
        }
    }
    
    private static string GetEditorState()
    {
        try
        {
            var result = new StringBuilder();
            result.AppendLine("Unity Editor State:");
            result.AppendLine($"Is Playing: {EditorApplication.isPlaying}");
            result.AppendLine($"Is Paused: {EditorApplication.isPaused}");
            result.AppendLine($"Is Compiling: {EditorApplication.isCompiling}");
            result.AppendLine($"Is Updating: {EditorApplication.isUpdating}");
            result.AppendLine($"Unity Version: {Application.unityVersion}");
            result.AppendLine($"Platform: {Application.platform}");
            
            // Get active scene info
            var activeScene = EditorSceneManager.GetActiveScene();
            result.AppendLine($"Active Scene: {activeScene.name}");
            result.AppendLine($"Scene Path: {activeScene.path}");
            result.AppendLine($"Scene Dirty: {activeScene.isDirty}");
            
            // Get selection info
            var selection = Selection.objects;
            result.AppendLine($"Selected Objects: {selection.Length}");
            if (selection.Length > 0)
            {
                result.AppendLine($"Active Object: {Selection.activeObject?.name ?? "None"}");
            }
            
            return result.ToString().Trim();
        }
        catch (Exception ex)
        {
            return $"Failed to get editor state: {ex.Message}";
        }
    }
    
    private static string SetActiveTool(Dictionary<string, object> input)
    {
        try
        {
            if (!input.ContainsKey("tool_name"))
            {
                return "Error: tool_name is required for set_active_tool action";
            }
            
            var toolName = input["tool_name"].ToString();
            
            // Map common tool names to Unity's built-in tools
            switch (toolName.ToLower())
            {
                case "move":
                case "movetool":
                    Tools.current = Tool.Move;
                    return "Activated Move tool";
                    
                case "rotate":
                case "rotatetool":
                    Tools.current = Tool.Rotate;
                    return "Activated Rotate tool";
                    
                case "scale":
                case "scaletool":
                    Tools.current = Tool.Scale;
                    return "Activated Scale tool";
                    
                case "rect":
                case "recttool":
                    Tools.current = Tool.Rect;
                    return "Activated Rect tool";
                    
                case "transform":
                case "transformtool":
                    Tools.current = Tool.Transform;
                    return "Activated Transform tool";
                    
                case "view":
                case "viewtool":
                    Tools.current = Tool.View;
                    return "Activated View tool";
                    
                case "none":
                    Tools.current = Tool.None;
                    return "Deactivated all tools";
                    
                default:
                    return $"Unknown tool: {toolName}. Available tools: Move, Rotate, Scale, Rect, Transform, View, None";
            }
        }
        catch (Exception ex)
        {
            return $"Failed to set active tool: {ex.Message}";
        }
    }
    
    private static string AddTag(Dictionary<string, object> input)
    {
        try
        {
            if (!input.ContainsKey("tag_name"))
            {
                return "Error: tag_name is required for add_tag action";
            }
            
            var tagName = input["tag_name"].ToString();
            
            // Check if tag already exists
            if (UnityEditorInternal.InternalEditorUtility.tags.Contains(tagName))
            {
                return $"Tag '{tagName}' already exists";
            }
            
            // Add tag using SerializedObject approach
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var tagsProp = tagManager.FindProperty("tags");
            
            // Find first empty slot
            bool added = false;
            for (int i = 0; i < tagsProp.arraySize; i++)
            {
                var tagProp = tagsProp.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(tagProp.stringValue))
                {
                    tagProp.stringValue = tagName;
                    added = true;
                    break;
                }
            }
            
            // If no empty slot, add new element
            if (!added)
            {
                tagsProp.InsertArrayElementAtIndex(tagsProp.arraySize);
                var newTagProp = tagsProp.GetArrayElementAtIndex(tagsProp.arraySize - 1);
                newTagProp.stringValue = tagName;
            }
            
            tagManager.ApplyModifiedProperties();
            return $"Successfully added tag: {tagName}";
        }
        catch (Exception ex)
        {
            return $"Failed to add tag: {ex.Message}";
        }
    }
    
    private static string AddLayer(Dictionary<string, object> input)
    {
        try
        {
            if (!input.ContainsKey("layer_name"))
            {
                return "Error: layer_name is required for add_layer action";
            }
            
            var layerName = input["layer_name"].ToString();
            
            // Check if layer already exists
            if (LayerMask.NameToLayer(layerName) != -1)
            {
                return $"Layer '{layerName}' already exists";
            }
            
            // Add layer using SerializedObject approach
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layersProp = tagManager.FindProperty("layers");
            
            // Find first empty slot (layers 8-31 are user-defined)
            bool added = false;
            for (int i = 8; i < layersProp.arraySize; i++)
            {
                var layerProp = layersProp.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(layerProp.stringValue))
                {
                    layerProp.stringValue = layerName;
                    added = true;
                    break;
                }
            }
            
            if (!added)
            {
                return "No available layer slots (layers 8-31 are full)";
            }
            
            tagManager.ApplyModifiedProperties();
            return $"Successfully added layer: {layerName}";
        }
        catch (Exception ex)
        {
            return $"Failed to add layer: {ex.Message}";
        }
    }
    
    private static string GetTags()
    {
        try
        {
            var tags = UnityEditorInternal.InternalEditorUtility.tags;
            
            var result = new StringBuilder();
            result.AppendLine($"Unity Tags ({tags.Length}):");
            result.AppendLine();
            
            for (int i = 0; i < tags.Length; i++)
            {
                result.AppendLine($"{i}: {tags[i]}");
            }
            
            return result.ToString().Trim();
        }
        catch (Exception ex)
        {
            return $"Failed to get tags: {ex.Message}";
        }
    }
    
    private static string GetLayers()
    {
        try
        {
            var result = new StringBuilder();
            result.AppendLine("Unity Layers:");
            result.AppendLine();
            
            // Get all layers (0-31)
            for (int i = 0; i < 32; i++)
            {
                var layerName = LayerMask.LayerToName(i);
                if (!string.IsNullOrEmpty(layerName))
                {
                    var isBuiltIn = i < 8;
                    var type = isBuiltIn ? "(Built-in)" : "(User)";
                    result.AppendLine($"{i}: {layerName} {type}");
                }
            }
            
            return result.ToString().Trim();
        }
        catch (Exception ex)
        {
            return $"Failed to get layers: {ex.Message}";
        }
    }
    
    private static string GetProjectSettings()
    {
        try
        {
            var result = new StringBuilder();
            result.AppendLine("Unity Project Settings:");
            result.AppendLine();
            
            // Company and Product Name
            result.AppendLine($"Company Name: {Application.companyName}");
            result.AppendLine($"Product Name: {Application.productName}");
            result.AppendLine($"Version: {Application.version}");
            result.AppendLine();
            
            // Build Settings
            result.AppendLine("Build Settings:");
            result.AppendLine($"Build Target: {EditorUserBuildSettings.activeBuildTarget}");
            result.AppendLine($"Development Build: {EditorUserBuildSettings.development}");
            result.AppendLine($"Script Debugging: {EditorUserBuildSettings.allowDebugging}");
            result.AppendLine();
            
            // Scene Count
            var sceneCount = EditorBuildSettings.scenes.Length;
            result.AppendLine($"Scenes in Build: {sceneCount}");
            
            // Quality Settings
            result.AppendLine($"Quality Level: {QualitySettings.GetQualityLevel()}");
            result.AppendLine($"Quality Names: {string.Join(", ", QualitySettings.names)}");
            
            return result.ToString().Trim();
        }
        catch (Exception ex)
        {
            return $"Failed to get project settings: {ex.Message}";
        }
    }
    
    private static string ExecuteMenuItem(Dictionary<string, object> input)
    {
        try
        {
            if (!input.ContainsKey("menu_path"))
            {
                return "Error: menu_path is required for execute_menu_item";
            }
            
            var menuPath = input["menu_path"].ToString();
            var validateOnly = input.ContainsKey("validate_only") && Convert.ToBoolean(input["validate_only"]);
            
            // Validate menu item exists
            var menuExists = Menu.GetEnabled(menuPath);
            
            if (validateOnly)
            {
                return menuExists ? 
                    $"Menu item exists and is enabled: {menuPath}" : 
                    $"Menu item does not exist or is disabled: {menuPath}";
            }
            
            if (!menuExists)
            {
                return $"Menu item does not exist or is disabled: {menuPath}";
            }
            
            // Execute the menu item
            EditorApplication.ExecuteMenuItem(menuPath);
            return $"Successfully executed menu item: {menuPath}";
        }
        catch (Exception ex)
        {
            return $"Failed to execute menu item: {ex.Message}";
        }
    }
} 