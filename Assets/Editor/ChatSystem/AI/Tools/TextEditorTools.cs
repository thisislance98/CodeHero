using UnityEngine;
using UnityEditor;
using UnityEditor.Compilation;
using System.Collections.Generic;
using System;
using Newtonsoft.Json;
using System.IO;
using System.Linq;

public static class TextEditorTools
{
    public static List<ClaudeTool> GetTextEditorTools()
    {
        return new List<ClaudeTool>
        {
            // Claude's built-in text editor tool for advanced script editing
            new ClaudeTool
            {
                type = "text_editor_20250429", // Claude 4 text editor
                name = "str_replace_based_edit_tool",
                description = null, // Explicitly null for built-in tools
                input_schema = null // Explicitly null for built-in tools
            }
        };
    }
    
    public static string ExecuteTextEditorTool(ClaudeToolUse toolUse)
    {
        var inputDict = JsonConvert.DeserializeObject<Dictionary<string, object>>(
            JsonConvert.SerializeObject(toolUse.input));
        
        switch (toolUse.name)
        {
            case "str_replace_based_edit_tool":
                Debug.Log("[ClaudeAI] TextEditorTools: Calling ExecuteTextEditorTool");
                return ExecuteTextEditorToolInternal(inputDict);
                
            default:
                return $"Unknown text editor tool: {toolUse.name}";
        }
    }
    
    private static string ExecuteTextEditorToolInternal(Dictionary<string, object> input)
    {
        try
        {
            if (!input.ContainsKey("command"))
            {
                return "Error: Text editor command not specified";
            }
            
            var command = input["command"].ToString();
            Debug.Log($"[ClaudeAI] Text editor command: {command}");
            
            switch (command)
            {
                case "view":
                    return HandleViewCommand(input);
                    
                case "str_replace":
                    return HandleStrReplaceCommand(input);
                    
                case "create":
                    return HandleCreateCommand(input);
                    
                case "insert":
                    return HandleInsertCommand(input);
                    
                default:
                    return $"Error: Unknown text editor command '{command}'";
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ClaudeAI] Text editor tool error: {ex.Message}\nStack trace: {ex.StackTrace}");
            return $"Text editor tool error: {ex.Message}";
        }
    }
    
    private static string HandleViewCommand(Dictionary<string, object> input)
    {
        try
        {
            if (!input.ContainsKey("path"))
            {
                return "Error: File path not specified";
            }
            
            var path = input["path"].ToString();
            
            string fullPath = GetFullPath(path);
            
            // Check if it's a directory
            if (Directory.Exists(fullPath))
            {
                var entries = new List<string>();
                var dirs = Directory.GetDirectories(fullPath).Select(d => Path.GetFileName(d) + "/");
                var files = Directory.GetFiles(fullPath).Select(f => Path.GetFileName(f));
                entries.AddRange(dirs);
                entries.AddRange(files);
                
                return $"Directory contents of '{path}':\n" + string.Join("\n", entries);
            }
            
            // Check if it's a file
            if (!File.Exists(fullPath))
            {
                return $"Error: File or directory not found: {path}";
            }
            
            var content = File.ReadAllText(fullPath);
            
            // Handle view_range if specified
            if (input.ContainsKey("view_range") && input["view_range"] is Newtonsoft.Json.Linq.JArray viewRangeArray)
            {
                var startLine = viewRangeArray[0].ToObject<int>();
                var endLine = viewRangeArray[1].ToObject<int>();
                
                var lines = content.Split('\n');
                if (endLine == -1) endLine = lines.Length;
                
                startLine = Math.Max(1, startLine);
                endLine = Math.Min(lines.Length, endLine);
                
                var selectedLines = new List<string>();
                for (int i = startLine - 1; i < endLine; i++)
                {
                    selectedLines.Add($"{i + 1}: {lines[i]}");
                }
                
                return string.Join("\n", selectedLines);
            }
            
            // Return full file with line numbers
            var numberedLines = content.Split('\n')
                .Select((line, index) => $"{index + 1}: {line}");
            
            return string.Join("\n", numberedLines);
        }
        catch (Exception ex)
        {
            return $"Error viewing file: {ex.Message}";
        }
    }
    
    private static string HandleStrReplaceCommand(Dictionary<string, object> input)
    {
        try
        {
            var path = input["path"].ToString();
            var oldStr = input["old_str"].ToString();
            var newStr = input["new_str"].ToString();
            
            ChatWindow.SendDebugMessage($"TextEditorTools str_replace: path='{path}', old_str length={oldStr?.Length ?? 0}, new_str length={newStr?.Length ?? 0}");
            
            // If this is a C# script, notify ChatWindow that Claude is performing a script operation
            if (Path.GetExtension(path).ToLower() == ".cs")
            {
                ChatWindow.NotifyClaudeScriptOperationStarted();
            }
            
            // Handle path format
            string fullPath = GetFullPath(path);
            
            if (!File.Exists(fullPath))
            {
                return $"Error: File not found: {path}\nFull path attempted: {fullPath}\nProject Assets path: {Application.dataPath}\nPlease use relative paths from Assets folder (e.g., 'Scripts/MyScript.cs')";
            }
            
            var content = File.ReadAllText(fullPath);
            var occurrences = CountOccurrences(content, oldStr);
            
            if (occurrences == 0)
            {
                return "Error: No match found for replacement. Please check your text and try again.";
            }
            
            if (occurrences > 1)
            {
                return $"Error: Found {occurrences} matches for replacement text. Please provide more context to make a unique match.";
            }
            
            var newContent = content.Replace(oldStr, newStr);
            File.WriteAllText(fullPath, newContent);
            
            // Use targeted import instead of full refresh
            var assetPath = "Assets" + fullPath.Substring(Application.dataPath.Length).Replace('\\', '/');
            AssetDatabase.ImportAsset(assetPath);
            
            // If this is a C# script, request compilation
            if (Path.GetExtension(path).ToLower() == ".cs")
            {
                Debug.Log("[ClaudeAI] TextEditorTools: Modified C# script, requesting compilation...");
                ChatWindow.SendDebugMessage("⏳ Waiting for compilation...");
                AssetDatabase.Refresh();
                CompilationPipeline.RequestScriptCompilation();
                Debug.Log("[ClaudeAI] TextEditorTools: Compilation requested");
            }
            
            var originalLength = oldStr?.Length ?? 0;
            var newLength = newStr?.Length ?? 0;
            var changeDelta = newLength - originalLength;
            
            var compilationNote = Path.GetExtension(path).ToLower() == ".cs" ? "🔄 File updated and compilation requested" : "🔄 File updated and imported";
            
            // If this was a C# script, notify completion
            if (Path.GetExtension(path).ToLower() == ".cs")
            {
                ChatWindow.NotifyClaudeScriptOperationCompleted();
            }
            
            return $"Successfully replaced text in '{Path.GetFileName(path)}'!\n" +
                   $"📝 Changed {originalLength} characters to {newLength} characters ({changeDelta:+#;-#;0} delta)\n" +
                   $"{compilationNote}";
        }
        catch (Exception ex)
        {
            return $"Error performing string replacement: {ex.Message}";
        }
    }
    
    private static string HandleCreateCommand(Dictionary<string, object> input)
    {
        try
        {
            var path = input["path"].ToString();
            var fileText = input["file_text"].ToString();
            
            ChatWindow.SendDebugMessage($"TextEditorTools create: path='{path}', fileText length={fileText?.Length ?? 0}");
            
            // If this is a C# script, notify ChatWindow that Claude is performing a script operation
            if (Path.GetExtension(path).ToLower() == ".cs")
            {
                ChatWindow.NotifyClaudeScriptOperationStarted();
            }
            
            string fullPath = GetFullPath(path);
            var directory = Path.GetDirectoryName(fullPath);
            
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            if (File.Exists(fullPath))
            {
                return $"Error: File already exists: {path}";
            }
            
            File.WriteAllText(fullPath, fileText);
            
            // Use targeted import instead of full refresh
            var assetPath = "Assets" + fullPath.Substring(Application.dataPath.Length).Replace('\\', '/');
            AssetDatabase.ImportAsset(assetPath);
            
            // If this is a C# script, request compilation
            if (Path.GetExtension(path).ToLower() == ".cs")
            {
                Debug.Log("[ClaudeAI] TextEditorTools: Created C# script, requesting compilation...");
                ChatWindow.SendDebugMessage("⏳ Waiting for compilation...");
                AssetDatabase.Refresh();
                CompilationPipeline.RequestScriptCompilation();
                Debug.Log("[ClaudeAI] TextEditorTools: Compilation requested");
            }
            
            var fileSize = fileText?.Length ?? 0;
            var lineCount = fileText?.Split('\n').Length ?? 0;
            
            var compilationNote = Path.GetExtension(path).ToLower() == ".cs" ? "🔄 File imported and compilation requested" : "🔄 File imported to Unity";
            
            // If this was a C# script, notify completion
            if (Path.GetExtension(path).ToLower() == ".cs")
            {
                ChatWindow.NotifyClaudeScriptOperationCompleted();
            }
            
            return $"Successfully created file '{Path.GetFileName(path)}'!\n" +
                   $"📁 Location: {path}\n" +
                   $"📝 Size: {fileSize} characters, {lineCount} lines\n" +
                   $"{compilationNote}";
        }
        catch (Exception ex)
        {
            return $"Error creating file: {ex.Message}";
        }
    }
    
    private static string HandleInsertCommand(Dictionary<string, object> input)
    {
        try
        {
            var path = input["path"].ToString();
            var insertLine = Convert.ToInt32(input["insert_line"]);
            var newStr = input["new_str"].ToString();
            
            // If this is a C# script, notify ChatWindow that Claude is performing a script operation
            if (Path.GetExtension(path).ToLower() == ".cs")
            {
                ChatWindow.NotifyClaudeScriptOperationStarted();
            }
            
            string fullPath = GetFullPath(path);
            
            if (!File.Exists(fullPath))
            {
                return $"Error: File not found: {path}\nFull path attempted: {fullPath}\nProject Assets path: {Application.dataPath}\nPlease use relative paths from Assets folder (e.g., 'Scripts/MyScript.cs')";
            }
            
            var lines = File.ReadAllLines(fullPath).ToList();
            
            if (insertLine < 0 || insertLine > lines.Count)
            {
                return $"Error: Invalid line number {insertLine}. File has {lines.Count} lines.";
            }
            
            lines.Insert(insertLine, newStr);
            File.WriteAllLines(fullPath, lines);
            
            // Use targeted import instead of full refresh
            var assetPath = "Assets" + fullPath.Substring(Application.dataPath.Length).Replace('\\', '/');
            AssetDatabase.ImportAsset(assetPath);
            
            // If this was a C# script, notify completion
            if (Path.GetExtension(path).ToLower() == ".cs")
            {
                ChatWindow.NotifyClaudeScriptOperationCompleted();
            }
            
            return $"Successfully inserted text at line {insertLine}.";
        }
        catch (Exception ex)
        {
            return $"Error inserting text: {ex.Message}";
        }
    }
    
    private static int CountOccurrences(string content, string searchString)
    {
        int count = 0;
        int index = 0;
        
        while ((index = content.IndexOf(searchString, index)) != -1)
        {
            count++;
            index += searchString.Length;
        }
        
        return count;
    }
    
    private static string GetFullPath(string path)
    {
        // Handle different path formats for Unity project files
        if (path == "." || path == "")
        {
            // Current directory is Assets folder
            return Application.dataPath;
        }
        else if (path.StartsWith("/Assets/") || path.StartsWith("Assets/"))
        {
            // Remove leading /Assets/ or Assets/ since Application.dataPath already points to Assets
            var relativePath = path.StartsWith("/Assets/") ? path.Substring(8) : path.Substring(7);
            return Path.Combine(Application.dataPath, relativePath);
        }
        else if (path.StartsWith("/"))
        {
            // Remove leading slash and treat as relative to Assets
            return Path.Combine(Application.dataPath, path.Substring(1));
        }
        else
        {
            // Treat as relative path from Assets folder
            return Path.Combine(Application.dataPath, path);
        }
    }

} 