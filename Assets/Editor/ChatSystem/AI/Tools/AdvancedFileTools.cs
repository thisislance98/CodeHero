using UnityEngine;
using System.Collections.Generic;
using System.Text;
using System;
using Newtonsoft.Json;
using System.IO;
using UnityEditor;

public static class AdvancedFileTools
{
    public static List<ClaudeTool> GetAdvancedFileTools()
    {
        return new List<ClaudeTool>
        {
            new ClaudeTool
            {
                name = "delete_file",
                description = "Delete a file or directory from the project. Use with caution as this operation cannot be undone.",
                input_schema = new ClaudeToolInputSchema
                {
                    properties = new Dictionary<string, ClaudeToolProperty>
                    {
                        ["path"] = new ClaudeToolProperty { type = "string", description = "Path to the file or directory to delete (relative to project root)" },
                        ["force"] = new ClaudeToolProperty { type = "string", description = "Force deletion even if directory is not empty (true/false, default: false)" },
                        ["refresh_assets"] = new ClaudeToolProperty { type = "string", description = "Whether to refresh Unity Asset Database after deletion (true/false, default: true)" }
                    },
                    required = new List<string> { "path" }
                }
            },
            new ClaudeTool
            {
                name = "move_file",
                description = "Move or rename a file or directory within the project.",
                input_schema = new ClaudeToolInputSchema
                {
                    properties = new Dictionary<string, ClaudeToolProperty>
                    {
                        ["source_path"] = new ClaudeToolProperty { type = "string", description = "Source path of the file or directory to move" },
                        ["destination_path"] = new ClaudeToolProperty { type = "string", description = "Destination path where the file or directory should be moved" },
                        ["overwrite"] = new ClaudeToolProperty { type = "string", description = "Whether to overwrite existing files (true/false, default: false)" },
                        ["refresh_assets"] = new ClaudeToolProperty { type = "string", description = "Whether to refresh Unity Asset Database after move (true/false, default: true)" }
                    },
                    required = new List<string> { "source_path", "destination_path" }
                }
            },
            new ClaudeTool
            {
                name = "copy_file",
                description = "Copy a file or directory to a new location in the project.",
                input_schema = new ClaudeToolInputSchema
                {
                    properties = new Dictionary<string, ClaudeToolProperty>
                    {
                        ["source_path"] = new ClaudeToolProperty { type = "string", description = "Source path of the file or directory to copy" },
                        ["destination_path"] = new ClaudeToolProperty { type = "string", description = "Destination path where the file or directory should be copied" },
                        ["overwrite"] = new ClaudeToolProperty { type = "string", description = "Whether to overwrite existing files (true/false, default: false)" },
                        ["refresh_assets"] = new ClaudeToolProperty { type = "string", description = "Whether to refresh Unity Asset Database after copy (true/false, default: true)" }
                    },
                    required = new List<string> { "source_path", "destination_path" }
                }
            }
        };
    }
    
    public static string ExecuteAdvancedFileTool(ClaudeToolUse toolUse)
    {
        var inputDict = JsonConvert.DeserializeObject<Dictionary<string, object>>(
            JsonConvert.SerializeObject(toolUse.input));
        
        switch (toolUse.name)
        {
            case "delete_file":
                Debug.Log("[ClaudeAI] AdvancedFileTools: Deleting file");
                return DeleteFile(inputDict);
                
            case "move_file":
                Debug.Log("[ClaudeAI] AdvancedFileTools: Moving file");
                return MoveFile(inputDict);
                
            case "copy_file":
                Debug.Log("[ClaudeAI] AdvancedFileTools: Copying file");
                return CopyFile(inputDict);
                
            default:
                return $"Unknown advanced file tool: {toolUse.name}";
        }
    }
    
    private static string DeleteFile(Dictionary<string, object> input)
    {
        try
        {
            var path = input["path"].ToString();
            var force = input.ContainsKey("force") ? input["force"].ToString().ToLower() == "true" : false;
            var refreshAssets = input.ContainsKey("refresh_assets") ? input["refresh_assets"].ToString().ToLower() == "true" : true;
            
            // Resolve path (can be relative to project root or Assets)
            var fullPath = ResolvePath(path);
            
            if (!File.Exists(fullPath) && !Directory.Exists(fullPath))
            {
                return $"❌ Path does not exist: {path}";
            }
            
            var result = new StringBuilder();
            result.AppendLine($"🗑️ Deleting: {path}");
            
            if (File.Exists(fullPath))
            {
                // Delete file
                File.Delete(fullPath);
                result.AppendLine("✅ File deleted successfully");
                
                // Delete .meta file if it exists
                var metaPath = fullPath + ".meta";
                if (File.Exists(metaPath))
                {
                    File.Delete(metaPath);
                    result.AppendLine("✅ Meta file deleted");
                }
            }
            else if (Directory.Exists(fullPath))
            {
                // Delete directory
                if (!force && Directory.GetFileSystemEntries(fullPath).Length > 0)
                {
                    return $"❌ Directory is not empty. Use force=true to delete non-empty directories: {path}";
                }
                
                Directory.Delete(fullPath, force);
                result.AppendLine("✅ Directory deleted successfully");
                
                // Delete .meta file if it exists
                var metaPath = fullPath + ".meta";
                if (File.Exists(metaPath))
                {
                    File.Delete(metaPath);
                    result.AppendLine("✅ Meta file deleted");
                }
            }
            
            if (refreshAssets)
            {
                AssetDatabase.Refresh();
                result.AppendLine("🔄 Asset Database refreshed");
            }
            
            return result.ToString().Trim();
        }
        catch (Exception ex)
        {
            return $"❌ Failed to delete file: {ex.Message}";
        }
    }
    
    private static string MoveFile(Dictionary<string, object> input)
    {
        try
        {
            var sourcePath = input["source_path"].ToString();
            var destinationPath = input["destination_path"].ToString();
            var overwrite = input.ContainsKey("overwrite") ? input["overwrite"].ToString().ToLower() == "true" : false;
            var refreshAssets = input.ContainsKey("refresh_assets") ? input["refresh_assets"].ToString().ToLower() == "true" : true;
            
            var fullSourcePath = ResolvePath(sourcePath);
            var fullDestinationPath = ResolvePath(destinationPath);
            
            if (!File.Exists(fullSourcePath) && !Directory.Exists(fullSourcePath))
            {
                return $"❌ Source path does not exist: {sourcePath}";
            }
            
            // Check if destination already exists
            if ((File.Exists(fullDestinationPath) || Directory.Exists(fullDestinationPath)) && !overwrite)
            {
                return $"❌ Destination already exists. Use overwrite=true to replace: {destinationPath}";
            }
            
            // Create destination directory if needed
            var destinationDir = Path.GetDirectoryName(fullDestinationPath);
            if (!Directory.Exists(destinationDir))
            {
                Directory.CreateDirectory(destinationDir);
            }
            
            var result = new StringBuilder();
            result.AppendLine($"📦 Moving: {sourcePath} → {destinationPath}");
            
            if (File.Exists(fullSourcePath))
            {
                // Move file
                if (File.Exists(fullDestinationPath) && overwrite)
                {
                    File.Delete(fullDestinationPath);
                }
                
                File.Move(fullSourcePath, fullDestinationPath);
                result.AppendLine("✅ File moved successfully");
                
                // Move .meta file if it exists
                var sourceMetaPath = fullSourcePath + ".meta";
                var destinationMetaPath = fullDestinationPath + ".meta";
                if (File.Exists(sourceMetaPath))
                {
                    if (File.Exists(destinationMetaPath) && overwrite)
                    {
                        File.Delete(destinationMetaPath);
                    }
                    File.Move(sourceMetaPath, destinationMetaPath);
                    result.AppendLine("✅ Meta file moved");
                }
            }
            else if (Directory.Exists(fullSourcePath))
            {
                // Move directory
                if (Directory.Exists(fullDestinationPath) && overwrite)
                {
                    Directory.Delete(fullDestinationPath, true);
                }
                
                Directory.Move(fullSourcePath, fullDestinationPath);
                result.AppendLine("✅ Directory moved successfully");
                
                // Move .meta file if it exists
                var sourceMetaPath = fullSourcePath + ".meta";
                var destinationMetaPath = fullDestinationPath + ".meta";
                if (File.Exists(sourceMetaPath))
                {
                    if (File.Exists(destinationMetaPath) && overwrite)
                    {
                        File.Delete(destinationMetaPath);
                    }
                    File.Move(sourceMetaPath, destinationMetaPath);
                    result.AppendLine("✅ Meta file moved");
                }
            }
            
            if (refreshAssets)
            {
                AssetDatabase.Refresh();
                result.AppendLine("🔄 Asset Database refreshed");
            }
            
            return result.ToString().Trim();
        }
        catch (Exception ex)
        {
            return $"❌ Failed to move file: {ex.Message}";
        }
    }
    
    private static string CopyFile(Dictionary<string, object> input)
    {
        try
        {
            var sourcePath = input["source_path"].ToString();
            var destinationPath = input["destination_path"].ToString();
            var overwrite = input.ContainsKey("overwrite") ? input["overwrite"].ToString().ToLower() == "true" : false;
            var refreshAssets = input.ContainsKey("refresh_assets") ? input["refresh_assets"].ToString().ToLower() == "true" : true;
            
            var fullSourcePath = ResolvePath(sourcePath);
            var fullDestinationPath = ResolvePath(destinationPath);
            
            if (!File.Exists(fullSourcePath) && !Directory.Exists(fullSourcePath))
            {
                return $"❌ Source path does not exist: {sourcePath}";
            }
            
            // Check if destination already exists
            if ((File.Exists(fullDestinationPath) || Directory.Exists(fullDestinationPath)) && !overwrite)
            {
                return $"❌ Destination already exists. Use overwrite=true to replace: {destinationPath}";
            }
            
            // Create destination directory if needed
            var destinationDir = Path.GetDirectoryName(fullDestinationPath);
            if (!Directory.Exists(destinationDir))
            {
                Directory.CreateDirectory(destinationDir);
            }
            
            var result = new StringBuilder();
            result.AppendLine($"📋 Copying: {sourcePath} → {destinationPath}");
            
            if (File.Exists(fullSourcePath))
            {
                // Copy file
                File.Copy(fullSourcePath, fullDestinationPath, overwrite);
                result.AppendLine("✅ File copied successfully");
                
                // Copy .meta file if it exists
                var sourceMetaPath = fullSourcePath + ".meta";
                var destinationMetaPath = fullDestinationPath + ".meta";
                if (File.Exists(sourceMetaPath))
                {
                    File.Copy(sourceMetaPath, destinationMetaPath, overwrite);
                    result.AppendLine("✅ Meta file copied");
                }
            }
            else if (Directory.Exists(fullSourcePath))
            {
                // Copy directory recursively
                CopyDirectory(fullSourcePath, fullDestinationPath, overwrite);
                result.AppendLine("✅ Directory copied successfully");
                
                // Copy .meta file if it exists
                var sourceMetaPath = fullSourcePath + ".meta";
                var destinationMetaPath = fullDestinationPath + ".meta";
                if (File.Exists(sourceMetaPath))
                {
                    File.Copy(sourceMetaPath, destinationMetaPath, overwrite);
                    result.AppendLine("✅ Meta file copied");
                }
            }
            
            if (refreshAssets)
            {
                AssetDatabase.Refresh();
                result.AppendLine("🔄 Asset Database refreshed");
            }
            
            return result.ToString().Trim();
        }
        catch (Exception ex)
        {
            return $"❌ Failed to copy file: {ex.Message}";
        }
    }
    
    private static void CopyDirectory(string sourceDir, string destinationDir, bool overwrite)
    {
        // Create destination directory
        if (!Directory.Exists(destinationDir))
        {
            Directory.CreateDirectory(destinationDir);
        }
        
        // Copy files
        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var fileName = Path.GetFileName(file);
            var destFile = Path.Combine(destinationDir, fileName);
            File.Copy(file, destFile, overwrite);
        }
        
        // Copy subdirectories recursively
        foreach (var subDir in Directory.GetDirectories(sourceDir))
        {
            var dirName = Path.GetFileName(subDir);
            var destSubDir = Path.Combine(destinationDir, dirName);
            CopyDirectory(subDir, destSubDir, overwrite);
        }
    }
    
    private static string ResolvePath(string path)
    {
        // If path is already absolute, return it
        if (Path.IsPathRooted(path))
        {
            return path;
        }
        
        // Try relative to project root first
        var projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
        var projectRelativePath = Path.Combine(projectRoot, path);
        
        if (File.Exists(projectRelativePath) || Directory.Exists(projectRelativePath))
        {
            return projectRelativePath;
        }
        
        // Try relative to Assets folder
        var assetsRelativePath = Path.Combine(Application.dataPath, path);
        
        if (File.Exists(assetsRelativePath) || Directory.Exists(assetsRelativePath))
        {
            return assetsRelativePath;
        }
        
        // Return project relative path as default (for creation operations)
        return projectRelativePath;
    }
} 