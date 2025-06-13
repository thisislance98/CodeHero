using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;
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
    
    public static async Task<string> ExecuteScriptToolAsync(ClaudeToolUse toolUse)
    {
        var inputDict = JsonConvert.DeserializeObject<Dictionary<string, object>>(
            JsonConvert.SerializeObject(toolUse.input));
        
        switch (toolUse.name)
        {
            case "create_script":
                Debug.Log("[ClaudeAI] ScriptTools: About to call CreateScript");
                var result = await CreateScript(inputDict);
                Debug.Log($"[ClaudeAI] ScriptTools: CreateScript returned: {result}");
                return result;
                
            default:
                return $"Unknown script tool: {toolUse.name}";
        }
    }
    
    private static async Task<string> CreateScript(Dictionary<string, object> input)
    {
        try
        {
            Debug.Log("[ClaudeAI] CreateScript: Starting script creation...");
            
            var scriptName = input["script_name"].ToString();
            var scriptContent = input["script_content"].ToString();
            var folderPath = input.ContainsKey("folder_path") ? input["folder_path"].ToString() : "Scripts";
            
            Debug.Log($"[ClaudeAI] CreateScript: Script name = '{scriptName}', folder = '{folderPath}'");
            
            var fullPath = Path.Combine(Application.dataPath, folderPath);
            Debug.Log($"[ClaudeAI] CreateScript: Full path = '{fullPath}'");
            
            if (!Directory.Exists(fullPath))
            {
                Debug.Log("[ClaudeAI] CreateScript: Creating directory...");
                Directory.CreateDirectory(fullPath);
            }
            
            var filePath = Path.Combine(fullPath, $"{scriptName}.cs");
            Debug.Log($"[ClaudeAI] CreateScript: Writing file to '{filePath}'");
            File.WriteAllText(filePath, scriptContent);
            Debug.Log("[ClaudeAI] CreateScript: File written successfully");
            
            // Use targeted import instead of full refresh to minimize compilation disruption
            var relativePath = Path.Combine("Assets", folderPath, $"{scriptName}.cs");
            Debug.Log($"[ClaudeAI] CreateScript: Importing asset '{relativePath}'");
            AssetDatabase.ImportAsset(relativePath);
            Debug.Log("[ClaudeAI] CreateScript: Asset import completed");
            
            // Return immediate success message with file path - don't wait for compilation
            var immediateResult = $"✅ Script '{scriptName}.cs' created successfully at {folderPath}/{scriptName}.cs\n\n⚙️ Unity is now compiling the script...";
            Debug.Log($"[ClaudeAI] CreateScript: Returning immediate result: {immediateResult}");
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