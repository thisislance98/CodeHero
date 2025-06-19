using UnityEngine;
using UnityEditor;
using UnityEditor.Compilation;
using System.Collections.Generic;
using System;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.IO;

public static class ScriptTools
{
    public static List<ClaudeTool> GetScriptTools()
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
            }
        };
    }
    
    public static string ExecuteScriptTool(ClaudeToolUse toolUse)
    {
        var inputDict = JsonConvert.DeserializeObject<Dictionary<string, object>>(
            JsonConvert.SerializeObject(toolUse.input));
        
        switch (toolUse.name)
        {
            case "create_script":
                Debug.Log("[ClaudeAI] ScriptTools: About to call CreateScript");
                var result = CreateScript(inputDict);
                Debug.Log($"[ClaudeAI] ScriptTools: CreateScript returned: {result}");
                return result;
                
            default:
                return $"Unknown script tool: {toolUse.name}";
        }
    }
    
    private static string CreateScript(Dictionary<string, object> input)
    {
        try
        {
            Debug.Log("[ClaudeAI] CreateScript: Starting script creation...");
            ChatWindow.SendDebugMessage("CreateScript: Starting script creation...");
            
            // Notify ChatWindow that Claude is performing a script operation
            ChatWindow.NotifyClaudeScriptOperationStarted();
            
            var scriptName = input["script_name"].ToString();
            var scriptContent = input["script_content"].ToString();
            var folderPath = input.ContainsKey("folder_path") ? input["folder_path"].ToString() : "Scripts";
            
            Debug.Log($"[ClaudeAI] CreateScript: Script name = '{scriptName}', folder = '{folderPath}'");
            ChatWindow.SendDebugMessage($"CreateScript: Script name = '{scriptName}', folder = '{folderPath}'");
            
            // Step 1: Prepare directory
            var fullPath = Path.Combine(Application.dataPath, folderPath);
            Debug.Log($"[ClaudeAI] CreateScript: Full path = '{fullPath}'");
            
            if (!Directory.Exists(fullPath))
            {
                Debug.Log("[ClaudeAI] CreateScript: Creating directory...");
                Directory.CreateDirectory(fullPath);
            }
            
            // Step 2: Write script file
            var filePath = Path.Combine(fullPath, $"{scriptName}.cs");
            Debug.Log($"[ClaudeAI] CreateScript: Writing file to '{filePath}'");
            
            // Check if file already exists to prevent duplicates
            if (File.Exists(filePath))
            {
                Debug.Log($"[ClaudeAI] CreateScript: File already exists, skipping creation: {filePath}");
                ChatWindow.SendDebugMessage($"CreateScript: File already exists, skipping creation: {filePath}");
                return $"Script '{scriptName}.cs' already exists at {folderPath}/{scriptName}.cs";
            }
            
            File.WriteAllText(filePath, scriptContent);
            Debug.Log("[ClaudeAI] CreateScript: File written successfully");
            ChatWindow.SendDebugMessage("CreateScript: File written successfully");
            
            // Step 3: Import asset
            var relativePath = Path.Combine("Assets", folderPath, $"{scriptName}.cs");
            Debug.Log($"[ClaudeAI] CreateScript: Importing asset '{relativePath}'");
            AssetDatabase.ImportAsset(relativePath);
            Debug.Log("[ClaudeAI] CreateScript: Asset import completed");
            
            // Step 4: Request compilation with multiple strategies for reliability
            Debug.Log("[ClaudeAI] CreateScript: Requesting Unity compilation...");
            ChatWindow.SendDebugMessage("⏳ Waiting for compilation...");
            
            // Strategy 1: Refresh asset database first
            AssetDatabase.Refresh();
            ChatWindow.SendDebugMessage("AssetDatabase.Refresh() completed");
            
            // Strategy 2: Use delayed calls to ensure file system changes are detected
            EditorApplication.delayCall += () =>
            {
                // Try regular RequestScriptCompilation first (faster incremental compilation)
                CompilationPipeline.RequestScriptCompilation();
                ChatWindow.SendDebugMessage("CompilationPipeline.RequestScriptCompilation() called");
                
                // Check compilation status after a brief delay
                EditorApplication.delayCall += () =>
                {
                    bool isCompiling = EditorApplication.isCompiling;
                    ChatWindow.SendDebugMessage($"EditorApplication.isCompiling after regular request: {isCompiling}");
                    
                    // Strategy 3: Fallback to CleanBuildCache if regular compilation didn't start
                    if (!isCompiling)
                    {
                        ChatWindow.SendDebugMessage("Regular compilation not started, trying CleanBuildCache as fallback...");
                        CompilationPipeline.RequestScriptCompilation(RequestScriptCompilationOptions.CleanBuildCache);
                        ChatWindow.SendDebugMessage("CompilationPipeline.RequestScriptCompilation(CleanBuildCache) called as fallback");
                        
                        // Final check
                        EditorApplication.delayCall += () =>
                        {
                            bool finalCompiling = EditorApplication.isCompiling;
                            ChatWindow.SendDebugMessage($"Final compilation check after fallback: {finalCompiling}");
                            
                            if (!finalCompiling)
                            {
                                ChatWindow.SendDebugMessage("⚠️ Warning: Neither compilation method triggered. Unity might not detect changes yet.");
                            }
                        };
                    }
                };
            };
            
            Debug.Log("[ClaudeAI] CreateScript: Compilation requested");
            
            // Return immediate success message with file details
            var scriptSize = scriptContent.Length;
            var lineCount = scriptContent.Split('\n').Length;
            var immediateResult = $"Script '{scriptName}.cs' created successfully!\n" +
                                $"📁 Location: {folderPath}/{scriptName}.cs\n" +
                                $"📝 Size: {scriptSize} characters, {lineCount} lines\n" +
                                $"🔄 Compilation requested";
            
            Debug.Log($"[ClaudeAI] CreateScript: Returning immediate result: {immediateResult}");
            ChatWindow.SendDebugMessage($"CreateScript: Returning result: {immediateResult}");
            
            // Notify ChatWindow that Claude's script operation is completed
            ChatWindow.NotifyClaudeScriptOperationCompleted();
            
            return immediateResult;
        }
        catch (Exception ex)
        {
            var error = $"Failed to create script: {ex.Message}";
            Debug.LogError($"[ClaudeAI] CreateScript: Exception occurred: {error}");
            Debug.LogError($"[ClaudeAI] CreateScript: Stack trace: {ex.StackTrace}");
            return error;
        }
    }
    
    public static async Task WaitForCompilationToComplete()
    {
        Debug.Log("[ClaudeAI] WaitForCompilationToComplete: Entering method");
        Debug.Log($"[ClaudeAI] WaitForCompilationToComplete: Initial compilation state = {EditorApplication.isCompiling}");
        
        // Give Unity a moment to start compilation if it's going to
        Debug.Log("[ClaudeAI] WaitForCompilationToComplete: Waiting 100ms for compilation to potentially start...");
        await Task.Delay(100);
        Debug.Log($"[ClaudeAI] WaitForCompilationToComplete: After initial delay, compilation state = {EditorApplication.isCompiling}");
        
        // If Unity is compiling, wait for it to finish
        if (EditorApplication.isCompiling)
        {
            Debug.Log("[ClaudeAI] WaitForCompilationToComplete: Unity is compiling, entering wait loop...");
            
            int waitTime = 0;
            const int checkInterval = 200; // Check every 200ms (more stable)
            const int statusInterval = 3000; // Print status every 3 seconds
            const int maxWaitTime = 30000; // Maximum wait time: 30 seconds
            
            // Wait for compilation to finish with timeout
            while (EditorApplication.isCompiling && waitTime < maxWaitTime)
            {
                Debug.Log($"[ClaudeAI] WaitForCompilationToComplete: In wait loop, waitTime = {waitTime}ms, still compiling = {EditorApplication.isCompiling}");
                await Task.Delay(checkInterval);
                waitTime += checkInterval;
                
                // Print status update every 3 seconds
                if (waitTime % statusInterval == 0)
                {
                    int seconds = waitTime / 1000;
                    Debug.Log($"[ClaudeAI] Still waiting for compilation... ({seconds}s elapsed)");
                }
            }
            
            Debug.Log($"[ClaudeAI] WaitForCompilationToComplete: Exited wait loop, waitTime = {waitTime}ms, maxWaitTime = {maxWaitTime}ms");
            
            if (waitTime >= maxWaitTime)
            {
                Debug.LogWarning("[ClaudeAI] Compilation wait timed out after 30 seconds. Continuing anyway.");
            }
            else
            {
                Debug.Log("[ClaudeAI] WaitForCompilationToComplete: Compilation finished, waiting additional 300ms for safety...");
                // Give a small additional delay to ensure compilation is fully complete
                await Task.Delay(300);
                
                int totalSeconds = waitTime / 1000;
                Debug.Log($"[ClaudeAI] Compilation completed after {totalSeconds}s.");
            }
        }
        else
        {
            // No compilation was needed or it finished very quickly
            Debug.Log("[ClaudeAI] WaitForCompilationToComplete: No compilation required or completed immediately.");
        }
        
        Debug.Log("[ClaudeAI] WaitForCompilationToComplete: Method completed, exiting");
    }
} 