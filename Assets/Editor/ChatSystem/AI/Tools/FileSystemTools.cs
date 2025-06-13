using UnityEngine;
using System.Collections.Generic;
using System.Text;
using System;
using Newtonsoft.Json;
using System.IO;

public static class FileSystemTools
{
    public static List<ClaudeTool> GetFileSystemTools()
    {
        return new List<ClaudeTool>
        {
            new ClaudeTool
            {
                name = "search_files",
                description = "Search for files in the Unity project. Can search by file extension, name pattern, or recursively explore directories.",
                input_schema = new ClaudeToolInputSchema
                {
                    properties = new Dictionary<string, ClaudeToolProperty>
                    {
                        ["search_pattern"] = new ClaudeToolProperty { type = "string", description = "File search pattern (e.g., '*.cs' for C# scripts, '*.prefab' for prefabs, or '*' for all files)" },
                        ["directory"] = new ClaudeToolProperty { type = "string", description = "Directory to search in, relative to Assets folder (default: search entire Assets folder)" },
                        ["recursive"] = new ClaudeToolProperty { type = "string", description = "Whether to search recursively in subdirectories (true/false, default: true)" }
                    },
                    required = new List<string> { "search_pattern" }
                }
            }
        };
    }
    
    public static string ExecuteFileSystemTool(ClaudeToolUse toolUse)
    {
        var inputDict = JsonConvert.DeserializeObject<Dictionary<string, object>>(
            JsonConvert.SerializeObject(toolUse.input));
        
        switch (toolUse.name)
        {
            case "search_files":
                Debug.Log("[ClaudeAI] FileSystemTools: Calling SearchFiles");
                return SearchFiles(inputDict);
                
            default:
                return $"Unknown file system tool: {toolUse.name}";
        }
    }
    
    private static string SearchFiles(Dictionary<string, object> input)
    {
        try
        {
            var searchPattern = input["search_pattern"].ToString();
            var directory = input.ContainsKey("directory") ? input["directory"].ToString() : "";
            var recursive = input.ContainsKey("recursive") ? input["recursive"].ToString().ToLower() == "true" : true;
            
            // Determine the search directory
            string searchDir;
            if (string.IsNullOrEmpty(directory))
            {
                searchDir = Application.dataPath;
            }
            else
            {
                searchDir = Path.Combine(Application.dataPath, directory);
            }
            
            if (!Directory.Exists(searchDir))
            {
                return $"Directory not found: {directory}";
            }
            
            // Perform the search
            var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var files = Directory.GetFiles(searchDir, searchPattern, searchOption);
            
            if (files.Length == 0)
            {
                return $"No files found matching pattern '{searchPattern}' in {(string.IsNullOrEmpty(directory) ? "Assets" : directory)}";
            }
            
            var result = new StringBuilder();
            result.AppendLine($"Found {files.Length} file(s) matching '{searchPattern}':");
            result.AppendLine();
            
            foreach (var file in files)
            {
                // Convert absolute path to relative path from Assets folder
                var relativePath = Path.GetRelativePath(Application.dataPath, file);
                var fileInfo = new FileInfo(file);
                
                result.AppendLine($"📄 {relativePath}");
                result.AppendLine($"   Size: {fileInfo.Length} bytes");
                result.AppendLine($"   Modified: {fileInfo.LastWriteTime:yyyy-MM-dd HH:mm:ss}");
                result.AppendLine();
            }
            
            return result.ToString().Trim();
        }
        catch (Exception ex)
        {
            return $"Failed to search files: {ex.Message}";
        }
    }
} 