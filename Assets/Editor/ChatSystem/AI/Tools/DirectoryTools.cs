using UnityEngine;
using System.Collections.Generic;
using System.Text;
using System;
using Newtonsoft.Json;
using System.IO;
using System.Linq;
using UnityEditor;

public static class DirectoryTools
{
    public static List<ClaudeTool> GetDirectoryTools()
    {
        return new List<ClaudeTool>
        {
            new ClaudeTool
            {
                name = "list_directory",
                description = "List contents of a directory with detailed information including file sizes, modification dates, and types.",
                input_schema = new ClaudeToolInputSchema
                {
                    properties = new Dictionary<string, ClaudeToolProperty>
                    {
                        ["path"] = new ClaudeToolProperty { type = "string", description = "Directory path to list (relative to project root, default: project root)" },
                        ["recursive"] = new ClaudeToolProperty { type = "string", description = "Whether to list recursively (true/false, default: false)" },
                        ["include_hidden"] = new ClaudeToolProperty { type = "string", description = "Whether to include hidden files and directories (true/false, default: false)" },
                        ["file_filter"] = new ClaudeToolProperty { type = "string", description = "File extension filter (e.g., '*.cs,*.js' or 'all' for all files)" },
                        ["max_depth"] = new ClaudeToolProperty { type = "string", description = "Maximum recursion depth (default: 10)" }
                    },
                    required = new List<string> { }
                }
            },
            new ClaudeTool
            {
                name = "create_directory",
                description = "Create a new directory structure, including parent directories if needed.",
                input_schema = new ClaudeToolInputSchema
                {
                    properties = new Dictionary<string, ClaudeToolProperty>
                    {
                        ["path"] = new ClaudeToolProperty { type = "string", description = "Directory path to create (relative to project root)" },
                        ["create_parents"] = new ClaudeToolProperty { type = "string", description = "Whether to create parent directories if they don't exist (true/false, default: true)" },
                        ["refresh_assets"] = new ClaudeToolProperty { type = "string", description = "Whether to refresh Unity Asset Database after creation (true/false, default: true)" }
                    },
                    required = new List<string> { "path" }
                }
            },
            new ClaudeTool
            {
                name = "analyze_directory",
                description = "Analyze a directory to get statistics about file types, sizes, and structure.",
                input_schema = new ClaudeToolInputSchema
                {
                    properties = new Dictionary<string, ClaudeToolProperty>
                    {
                        ["path"] = new ClaudeToolProperty { type = "string", description = "Directory path to analyze (relative to project root, default: project root)" },
                        ["include_subdirs"] = new ClaudeToolProperty { type = "string", description = "Whether to include subdirectories in analysis (true/false, default: true)" },
                        ["group_by_extension"] = new ClaudeToolProperty { type = "string", description = "Whether to group results by file extension (true/false, default: true)" }
                    },
                    required = new List<string> { }
                }
            }
        };
    }
    
    public static string ExecuteDirectoryTool(ClaudeToolUse toolUse)
    {
        var inputDict = JsonConvert.DeserializeObject<Dictionary<string, object>>(
            JsonConvert.SerializeObject(toolUse.input));
        
        switch (toolUse.name)
        {
            case "list_directory":
                UnityEngine.Debug.Log("[ClaudeAI] DirectoryTools: Listing directory");
                return ListDirectory(inputDict);
                
            case "create_directory":
                UnityEngine.Debug.Log("[ClaudeAI] DirectoryTools: Creating directory");
                return CreateDirectory(inputDict);
                
            case "analyze_directory":
                UnityEngine.Debug.Log("[ClaudeAI] DirectoryTools: Analyzing directory");
                return AnalyzeDirectory(inputDict);
                
            default:
                return $"Unknown directory tool: {toolUse.name}";
        }
    }
    
    private static string ListDirectory(Dictionary<string, object> input)
    {
        try
        {
            var path = input.ContainsKey("path") ? input["path"].ToString() : "";
            var recursive = input.ContainsKey("recursive") ? input["recursive"].ToString().ToLower() == "true" : false;
            var includeHidden = input.ContainsKey("include_hidden") ? input["include_hidden"].ToString().ToLower() == "true" : false;
            var fileFilter = input.ContainsKey("file_filter") ? input["file_filter"].ToString() : "all";
            var maxDepth = input.ContainsKey("max_depth") ? int.Parse(input["max_depth"].ToString()) : 10;
            
            var fullPath = ResolvePath(path);
            
            if (!Directory.Exists(fullPath))
            {
                return $"❌ Directory does not exist: {path}";
            }
            
            var result = new StringBuilder();
            result.AppendLine($"📁 Directory listing: {(string.IsNullOrEmpty(path) ? "Project Root" : path)}");
            result.AppendLine($"🔍 Mode: {(recursive ? "Recursive" : "Top-level only")}");
            result.AppendLine();
            
            var entries = GetDirectoryEntries(fullPath, recursive, includeHidden, fileFilter, maxDepth, 0);
            
            if (entries.Count == 0)
            {
                result.AppendLine("📭 Directory is empty or no files match the filter.");
                return result.ToString().Trim();
            }
            
            // Group by type
            var directories = entries.Where(e => e.IsDirectory).OrderBy(e => e.Name).ToList();
            var files = entries.Where(e => !e.IsDirectory).OrderBy(e => e.Name).ToList();
            
            // Show directories first
            if (directories.Any())
            {
                result.AppendLine("📂 Directories:");
                foreach (var dir in directories)
                {
                    result.AppendLine($"  {dir.RelativePath}/ {dir.IndentPrefix}({dir.ItemCount} items, Modified: {dir.LastModified:yyyy-MM-dd HH:mm})");
                }
                result.AppendLine();
            }
            
            // Show files
            if (files.Any())
            {
                result.AppendLine("📄 Files:");
                foreach (var file in files)
                {
                    var sizeStr = FormatFileSize(file.Size);
                    result.AppendLine($"  {file.RelativePath} {file.IndentPrefix}({sizeStr}, Modified: {file.LastModified:yyyy-MM-dd HH:mm})");
                }
                result.AppendLine();
            }
            
            result.AppendLine($"📊 Summary: {directories.Count} directories, {files.Count} files");
            
            return result.ToString().Trim();
        }
        catch (Exception ex)
        {
            return $"❌ Failed to list directory: {ex.Message}";
        }
    }
    
    private static string CreateDirectory(Dictionary<string, object> input)
    {
        try
        {
            var path = input["path"].ToString();
            var createParents = input.ContainsKey("create_parents") ? input["create_parents"].ToString().ToLower() == "true" : true;
            var refreshAssets = input.ContainsKey("refresh_assets") ? input["refresh_assets"].ToString().ToLower() == "true" : true;
            
            var fullPath = ResolvePath(path);
            
            if (Directory.Exists(fullPath))
            {
                return $"✅ Directory already exists: {path}";
            }
            
            var result = new StringBuilder();
            result.AppendLine($"📁 Creating directory: {path}");
            
            if (createParents)
            {
                Directory.CreateDirectory(fullPath);
                result.AppendLine("✅ Directory created successfully (including parent directories)");
            }
            else
            {
                var parentDir = Path.GetDirectoryName(fullPath);
                if (!Directory.Exists(parentDir))
                {
                    return $"❌ Parent directory does not exist: {parentDir}. Use create_parents=true to create parent directories.";
                }
                
                Directory.CreateDirectory(fullPath);
                result.AppendLine("✅ Directory created successfully");
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
            return $"❌ Failed to create directory: {ex.Message}";
        }
    }
    
    private static string AnalyzeDirectory(Dictionary<string, object> input)
    {
        try
        {
            var path = input.ContainsKey("path") ? input["path"].ToString() : "";
            var includeSubdirs = input.ContainsKey("include_subdirs") ? input["include_subdirs"].ToString().ToLower() == "true" : true;
            var groupByExtension = input.ContainsKey("group_by_extension") ? input["group_by_extension"].ToString().ToLower() == "true" : true;
            
            var fullPath = ResolvePath(path);
            
            if (!Directory.Exists(fullPath))
            {
                return $"❌ Directory does not exist: {path}";
            }
            
            var result = new StringBuilder();
            result.AppendLine($"📊 Directory Analysis: {(string.IsNullOrEmpty(path) ? "Project Root" : path)}");
            result.AppendLine();
            
            var searchOption = includeSubdirs ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var files = Directory.GetFiles(fullPath, "*", searchOption)
                .Where(f => !IsIgnoredPath(f))
                .Select(f => new FileInfo(f))
                .ToList();
            
            var directories = Directory.GetDirectories(fullPath, "*", searchOption)
                .Where(d => !IsIgnoredPath(d))
                .Count();
            
            // Basic statistics
            var totalSize = files.Sum(f => f.Length);
            var averageSize = files.Count > 0 ? totalSize / files.Count : 0;
            
            result.AppendLine("📈 Basic Statistics:");
            result.AppendLine($"  Total Files: {files.Count:N0}");
            result.AppendLine($"  Total Directories: {directories:N0}");
            result.AppendLine($"  Total Size: {FormatFileSize(totalSize)}");
            result.AppendLine($"  Average File Size: {FormatFileSize(averageSize)}");
            result.AppendLine();
            
            if (groupByExtension && files.Any())
            {
                // Group by extension
                var extensionGroups = files
                    .GroupBy(f => Path.GetExtension(f.FullName).ToLower())
                    .OrderByDescending(g => g.Sum(f => f.Length))
                    .Take(15) // Top 15 extensions
                    .ToList();
                
                result.AppendLine("📋 File Types (by size):");
                foreach (var group in extensionGroups)
                {
                    var extension = string.IsNullOrEmpty(group.Key) ? "(no extension)" : group.Key;
                    var count = group.Count();
                    var size = group.Sum(f => f.Length);
                    var percentage = totalSize > 0 ? (size * 100.0 / totalSize) : 0;
                    
                    result.AppendLine($"  {extension}: {count:N0} files, {FormatFileSize(size)} ({percentage:F1}%)");
                }
                result.AppendLine();
            }
            
            // Largest files
            var largestFiles = files
                .OrderByDescending(f => f.Length)
                .Take(10)
                .ToList();
            
            if (largestFiles.Any())
            {
                result.AppendLine("📏 Largest Files:");
                var projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
                foreach (var file in largestFiles)
                {
                    var relativePath = Path.GetRelativePath(projectRoot, file.FullName);
                    result.AppendLine($"  {FormatFileSize(file.Length)} - {relativePath}");
                }
                result.AppendLine();
            }
            
            // Recently modified files
            var recentFiles = files
                .OrderByDescending(f => f.LastWriteTime)
                .Take(5)
                .ToList();
            
            if (recentFiles.Any())
            {
                result.AppendLine("🕒 Recently Modified:");
                var projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
                foreach (var file in recentFiles)
                {
                    var relativePath = Path.GetRelativePath(projectRoot, file.FullName);
                    result.AppendLine($"  {file.LastWriteTime:yyyy-MM-dd HH:mm} - {relativePath}");
                }
            }
            
            return result.ToString().Trim();
        }
        catch (Exception ex)
        {
            return $"❌ Failed to analyze directory: {ex.Message}";
        }
    }
    
    private static List<DirectoryEntry> GetDirectoryEntries(string path, bool recursive, bool includeHidden, string fileFilter, int maxDepth, int currentDepth)
    {
        var entries = new List<DirectoryEntry>();
        
        if (currentDepth >= maxDepth)
            return entries;
        
        try
        {
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            
            // Get directories
            var directories = Directory.GetDirectories(path)
                .Where(d => includeHidden || !IsHidden(d))
                .Where(d => !IsIgnoredPath(d));
            
            foreach (var dir in directories)
            {
                var dirInfo = new DirectoryInfo(dir);
                var itemCount = Directory.GetFileSystemEntries(dir).Length;
                var relativePath = Path.GetRelativePath(projectRoot, dir);
                var indentPrefix = new string(' ', currentDepth * 2);
                
                entries.Add(new DirectoryEntry
                {
                    Name = dirInfo.Name,
                    RelativePath = relativePath,
                    IsDirectory = true,
                    Size = 0,
                    LastModified = dirInfo.LastWriteTime,
                    ItemCount = itemCount,
                    IndentPrefix = indentPrefix
                });
                
                if (recursive)
                {
                    entries.AddRange(GetDirectoryEntries(dir, recursive, includeHidden, fileFilter, maxDepth, currentDepth + 1));
                }
            }
            
            // Get files
            var filePatterns = fileFilter == "all" 
                ? new[] { "*" }
                : fileFilter.Split(',').Select(p => p.Trim()).ToArray();
            
            foreach (var pattern in filePatterns)
            {
                var files = Directory.GetFiles(path, pattern)
                    .Where(f => includeHidden || !IsHidden(f))
                    .Where(f => !IsIgnoredPath(f));
                
                foreach (var file in files)
                {
                    var fileInfo = new FileInfo(file);
                    var relativePath = Path.GetRelativePath(projectRoot, file);
                    var indentPrefix = new string(' ', currentDepth * 2);
                    
                    entries.Add(new DirectoryEntry
                    {
                        Name = fileInfo.Name,
                        RelativePath = relativePath,
                        IsDirectory = false,
                        Size = fileInfo.Length,
                        LastModified = fileInfo.LastWriteTime,
                        ItemCount = 0,
                        IndentPrefix = indentPrefix
                    });
                }
            }
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogWarning($"Error processing directory {path}: {ex.Message}");
        }
        
        return entries;
    }
    
    private static bool IsHidden(string path)
    {
        try
        {
            var attributes = File.GetAttributes(path);
            return (attributes & FileAttributes.Hidden) == FileAttributes.Hidden;
        }
        catch
        {
            return false;
        }
    }
    
    private static bool IsIgnoredPath(string path)
    {
        var ignoredDirs = new[] { "Library", "Temp", "Obj", "obj", "bin", "Logs", ".git", ".vs", ".vscode", "node_modules" };
        return ignoredDirs.Any(dir => path.Contains(Path.DirectorySeparatorChar + dir + Path.DirectorySeparatorChar));
    }
    
    private static string FormatFileSize(long bytes)
    {
        string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
        int counter = 0;
        decimal number = bytes;
        
        while (Math.Round(number / 1024) >= 1)
        {
            number /= 1024;
            counter++;
        }
        
        return $"{number:n1} {suffixes[counter]}";
    }
    
    private static string ResolvePath(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
        }
        
        if (Path.IsPathRooted(path))
        {
            return path;
        }
        
        var projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
        return Path.Combine(projectRoot, path);
    }
    
    private class DirectoryEntry
    {
        public string Name { get; set; }
        public string RelativePath { get; set; }
        public bool IsDirectory { get; set; }
        public long Size { get; set; }
        public DateTime LastModified { get; set; }
        public int ItemCount { get; set; }
        public string IndentPrefix { get; set; }
    }
} 