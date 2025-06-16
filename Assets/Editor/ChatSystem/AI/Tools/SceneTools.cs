using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;
using System.Collections.Generic;
using System.Text;
using System;
using Newtonsoft.Json;
using System.IO;
using System.Linq;

public static class SceneTools
{
    public static List<ClaudeTool> GetSceneTools()
    {
        return new List<ClaudeTool>
        {
            new ClaudeTool
            {
                name = "manage_scene",
                description = "Manage Unity scenes including load, save, create, get hierarchy, and build settings operations.",
                input_schema = new ClaudeToolInputSchema
                {
                    properties = new Dictionary<string, ClaudeToolProperty>
                    {
                        ["action"] = new ClaudeToolProperty { type = "string", description = "Operation to perform: 'load', 'save', 'save_as', 'create', 'get_hierarchy', 'get_active', 'add_to_build', 'remove_from_build', 'get_build_settings'" },
                        ["name"] = new ClaudeToolProperty { type = "string", description = "Scene name (without extension) for create/load/save operations" },
                        ["path"] = new ClaudeToolProperty { type = "string", description = "Scene path relative to Assets folder (default: 'Assets/')" },
                        ["build_index"] = new ClaudeToolProperty { type = "integer", description = "Build index for build settings operations" },
                        ["hierarchy_depth"] = new ClaudeToolProperty { type = "integer", description = "Maximum depth for hierarchy traversal (default: unlimited)" },
                        ["include_inactive"] = new ClaudeToolProperty { type = "boolean", description = "Include inactive GameObjects in hierarchy (default: true)" }
                    },
                    required = new List<string> { "action" }
                }
            }
        };
    }
    
    public static string ExecuteSceneTool(ClaudeToolUse toolUse)
    {
        var inputDict = JsonConvert.DeserializeObject<Dictionary<string, object>>(
            JsonConvert.SerializeObject(toolUse.input));
        
        switch (toolUse.name)
        {
            case "manage_scene":
                return ManageScene(inputDict);
            default:
                return $"Unknown scene tool: {toolUse.name}";
        }
    }
    
    private static string ManageScene(Dictionary<string, object> input)
    {
        try
        {
            var action = input["action"].ToString().ToLower();
            
            switch (action)
            {
                case "load":
                    return LoadScene(input);
                case "save":
                    return SaveScene();
                case "save_as":
                    return SaveSceneAs(input);
                case "create":
                    return CreateScene(input);
                case "get_hierarchy":
                    return GetSceneHierarchy(input);
                case "get_active":
                    return GetActiveScene();
                case "add_to_build":
                    return AddToBuildSettings(input);
                case "remove_from_build":
                    return RemoveFromBuildSettings(input);
                case "get_build_settings":
                    return GetBuildSettings();
                default:
                    return $"Unknown scene action: {action}";
            }
        }
        catch (Exception ex)
        {
            return $"Scene operation failed: {ex.Message}";
        }
    }
    
    private static string LoadScene(Dictionary<string, object> input)
    {
        try
        {
            string scenePath;
            
            if (input.ContainsKey("build_index"))
            {
                var buildIndex = Convert.ToInt32(input["build_index"]);
                var buildScenes = EditorBuildSettings.scenes;
                
                if (buildIndex < 0 || buildIndex >= buildScenes.Length)
                {
                    return $"Invalid build index: {buildIndex}. Available indices: 0-{buildScenes.Length - 1}";
                }
                
                scenePath = buildScenes[buildIndex].path;
            }
            else
            {
                if (!input.ContainsKey("name"))
                {
                    return "Error: Either 'name' or 'build_index' is required for load operation";
                }
                
                var name = input["name"].ToString();
                var path = input.ContainsKey("path") ? input["path"].ToString() : "Assets/";
                
                if (!path.StartsWith("Assets/"))
                {
                    path = "Assets/" + path;
                }
                
                scenePath = Path.Combine(path, name + ".unity");
            }
            
            if (!File.Exists(scenePath))
            {
                return $"Scene file not found: {scenePath}";
            }
            
            // Check if scene is dirty and needs saving
            var currentScene = EditorSceneManager.GetActiveScene();
            if (currentScene.isDirty)
            {
                if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    EditorSceneManager.OpenScene(scenePath);
                    return $"Successfully loaded scene: {scenePath}";
                }
                else
                {
                    return "Scene load cancelled by user";
                }
            }
            else
            {
                EditorSceneManager.OpenScene(scenePath);
                return $"Successfully loaded scene: {scenePath}";
            }
        }
        catch (Exception ex)
        {
            return $"Failed to load scene: {ex.Message}";
        }
    }
    
    private static string SaveScene()
    {
        try
        {
            var activeScene = EditorSceneManager.GetActiveScene();
            
            if (string.IsNullOrEmpty(activeScene.path))
            {
                return "Error: Active scene has no path. Use 'save_as' action to save with a specific path.";
            }
            
            if (EditorSceneManager.SaveScene(activeScene))
            {
                return $"Successfully saved scene: {activeScene.path}";
            }
            
            return "Failed to save scene";
        }
        catch (Exception ex)
        {
            return $"Failed to save scene: {ex.Message}";
        }
    }
    
    private static string SaveSceneAs(Dictionary<string, object> input)
    {
        try
        {
            if (!input.ContainsKey("name"))
            {
                return "Error: 'name' is required for save_as operation";
            }
            
            var name = input["name"].ToString();
            var path = input.ContainsKey("path") ? input["path"].ToString() : "Assets/";
            
            if (!path.StartsWith("Assets/"))
            {
                path = "Assets/" + path;
            }
            
            var scenePath = Path.Combine(path, name + ".unity");
            var activeScene = EditorSceneManager.GetActiveScene();
            
            // Ensure directory exists
            var directory = Path.GetDirectoryName(scenePath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            if (EditorSceneManager.SaveScene(activeScene, scenePath))
            {
                AssetDatabase.Refresh();
                return $"Successfully saved scene as: {scenePath}";
            }
            
            return $"Failed to save scene as: {scenePath}";
        }
        catch (Exception ex)
        {
            return $"Failed to save scene as: {ex.Message}";
        }
    }
    
    private static string CreateScene(Dictionary<string, object> input)
    {
        try
        {
            if (!input.ContainsKey("name"))
            {
                return "Error: 'name' is required for create operation";
            }
            
            var name = input["name"].ToString();
            var path = input.ContainsKey("path") ? input["path"].ToString() : "Assets/";
            
            if (!path.StartsWith("Assets/"))
            {
                path = "Assets/" + path;
            }
            
            var scenePath = Path.Combine(path, name + ".unity");
            
            // Check if scene already exists
            if (File.Exists(scenePath))
            {
                return $"Scene already exists at: {scenePath}";
            }
            
            // Ensure directory exists
            var directory = Path.GetDirectoryName(scenePath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            // Create new scene
            var newScene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            
            // Save the new scene
            if (EditorSceneManager.SaveScene(newScene, scenePath))
            {
                AssetDatabase.Refresh();
                return $"Successfully created new scene: {scenePath}";
            }
            
            return $"Failed to create scene: {scenePath}";
        }
        catch (Exception ex)
        {
            return $"Failed to create scene: {ex.Message}";
        }
    }
    
    private static string GetSceneHierarchy(Dictionary<string, object> input)
    {
        try
        {
            var maxDepth = input.ContainsKey("hierarchy_depth") ? Convert.ToInt32(input["hierarchy_depth"]) : -1;
            var includeInactive = input.ContainsKey("include_inactive") ? Convert.ToBoolean(input["include_inactive"]) : true;
            
            var activeScene = EditorSceneManager.GetActiveScene();
            var rootObjects = activeScene.GetRootGameObjects();
            
            var result = new StringBuilder();
            result.AppendLine($"Scene Hierarchy for: {activeScene.name}");
            result.AppendLine($"Path: {activeScene.path}");
            result.AppendLine($"Root GameObjects: {rootObjects.Length}");
            result.AppendLine();
            
            foreach (var rootObj in rootObjects)
            {
                if (!includeInactive && !rootObj.activeInHierarchy)
                    continue;
                    
                BuildHierarchyString(rootObj.transform, result, 0, maxDepth, includeInactive);
            }
            
            return result.ToString().Trim();
        }
        catch (Exception ex)
        {
            return $"Failed to get scene hierarchy: {ex.Message}";
        }
    }
    
    private static void BuildHierarchyString(Transform transform, StringBuilder result, int currentDepth, int maxDepth, bool includeInactive)
    {
        if (maxDepth >= 0 && currentDepth > maxDepth)
            return;
            
        if (!includeInactive && !transform.gameObject.activeInHierarchy)
            return;
        
        // Add indentation using string multiplication instead of char constructor
        var indent = string.Concat(Enumerable.Repeat("  ", currentDepth));
        var activeStatus = transform.gameObject.activeInHierarchy ? "✓" : "✗";
        var components = transform.GetComponents<Component>().Where(c => c != null && !(c is Transform)).ToArray();
        
        result.AppendLine($"{indent}{activeStatus} {transform.name}");
        
        if (components.Length > 0)
        {
            result.AppendLine($"{indent}   Components: {string.Join(", ", components.Select(c => c.GetType().Name))}");
        }
        
        // Recursively add children
        for (int i = 0; i < transform.childCount; i++)
        {
            BuildHierarchyString(transform.GetChild(i), result, currentDepth + 1, maxDepth, includeInactive);
        }
    }
    
    private static string GetActiveScene()
    {
        try
        {
            var activeScene = EditorSceneManager.GetActiveScene();
            var loadedScenes = new List<Scene>();
            
            for (int i = 0; i < EditorSceneManager.sceneCount; i++)
            {
                loadedScenes.Add(EditorSceneManager.GetSceneAt(i));
            }
            
            var result = new StringBuilder();
            result.AppendLine("Active Scene Information:");
            result.AppendLine($"Name: {activeScene.name}");
            result.AppendLine($"Path: {activeScene.path}");
            result.AppendLine($"Is Dirty: {activeScene.isDirty}");
            result.AppendLine($"Is Loaded: {activeScene.isLoaded}");
            result.AppendLine($"Root GameObject Count: {activeScene.rootCount}");
            result.AppendLine();
            
            if (loadedScenes.Count > 1)
            {
                result.AppendLine($"Other Loaded Scenes ({loadedScenes.Count - 1}):");
                foreach (var scene in loadedScenes)
                {
                    if (scene != activeScene)
                    {
                        result.AppendLine($"  - {scene.name} ({scene.path})");
                    }
                }
            }
            
            return result.ToString().Trim();
        }
        catch (Exception ex)
        {
            return $"Failed to get active scene info: {ex.Message}";
        }
    }
    
    private static string AddToBuildSettings(Dictionary<string, object> input)
    {
        try
        {
            string scenePath;
            
            if (input.ContainsKey("name"))
            {
                var name = input["name"].ToString();
                var path = input.ContainsKey("path") ? input["path"].ToString() : "Assets/";
                
                if (!path.StartsWith("Assets/"))
                {
                    path = "Assets/" + path;
                }
                
                scenePath = Path.Combine(path, name + ".unity");
            }
            else
            {
                // Use active scene
                scenePath = EditorSceneManager.GetActiveScene().path;
            }
            
            if (!File.Exists(scenePath))
            {
                return $"Scene file not found: {scenePath}";
            }
            
            var buildScenes = EditorBuildSettings.scenes.ToList();
            
            // Check if scene is already in build settings
            if (buildScenes.Any(s => s.path == scenePath))
            {
                return $"Scene already in build settings: {scenePath}";
            }
            
            // Add scene to build settings
            buildScenes.Add(new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = buildScenes.ToArray();
            
            return $"Successfully added scene to build settings: {scenePath} (Index: {buildScenes.Count - 1})";
        }
        catch (Exception ex)
        {
            return $"Failed to add scene to build settings: {ex.Message}";
        }
    }
    
    private static string RemoveFromBuildSettings(Dictionary<string, object> input)
    {
        try
        {
            var buildScenes = EditorBuildSettings.scenes.ToList();
            
            if (input.ContainsKey("build_index"))
            {
                var buildIndex = Convert.ToInt32(input["build_index"]);
                
                if (buildIndex < 0 || buildIndex >= buildScenes.Count)
                {
                    return $"Invalid build index: {buildIndex}. Available indices: 0-{buildScenes.Count - 1}";
                }
                
                var removedScene = buildScenes[buildIndex];
                buildScenes.RemoveAt(buildIndex);
                EditorBuildSettings.scenes = buildScenes.ToArray();
                
                return $"Successfully removed scene from build settings: {removedScene.path}";
            }
            else if (input.ContainsKey("name"))
            {
                var name = input["name"].ToString();
                var path = input.ContainsKey("path") ? input["path"].ToString() : "Assets/";
                
                if (!path.StartsWith("Assets/"))
                {
                    path = "Assets/" + path;
                }
                
                var scenePath = Path.Combine(path, name + ".unity");
                var sceneIndex = buildScenes.FindIndex(s => s.path == scenePath);
                
                if (sceneIndex == -1)
                {
                    return $"Scene not found in build settings: {scenePath}";
                }
                
                buildScenes.RemoveAt(sceneIndex);
                EditorBuildSettings.scenes = buildScenes.ToArray();
                
                return $"Successfully removed scene from build settings: {scenePath}";
            }
            else
            {
                return "Error: Either 'build_index' or 'name' is required for remove operation";
            }
        }
        catch (Exception ex)
        {
            return $"Failed to remove scene from build settings: {ex.Message}";
        }
    }
    
    private static string GetBuildSettings()
    {
        try
        {
            var buildScenes = EditorBuildSettings.scenes;
            
            if (buildScenes.Length == 0)
            {
                return "No scenes in build settings";
            }
            
            var result = new StringBuilder();
            result.AppendLine($"Build Settings ({buildScenes.Length} scenes):");
            result.AppendLine();
            
            for (int i = 0; i < buildScenes.Length; i++)
            {
                var scene = buildScenes[i];
                var status = scene.enabled ? "✓" : "✗";
                var exists = File.Exists(scene.path) ? "📁" : "❌";
                
                result.AppendLine($"{i}: {status} {exists} {scene.path}");
            }
            
            result.AppendLine();
            result.AppendLine("Legend:");
            result.AppendLine("✓/✗ = Enabled/Disabled in build");
            result.AppendLine("📁/❌ = File exists/missing");
            
            return result.ToString().Trim();
        }
        catch (Exception ex)
        {
            return $"Failed to get build settings: {ex.Message}";
        }
    }
} 