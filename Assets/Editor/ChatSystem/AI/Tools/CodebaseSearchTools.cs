using UnityEngine;
using System.Collections.Generic;
using System.Text;
using System;
using Newtonsoft.Json;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

public static class CodebaseSearchTools
{
    public static List<ClaudeTool> GetCodebaseSearchTools()
    {
        return new List<ClaudeTool>
        {
            new ClaudeTool
            {
                name = "search_codebase",
                description = "Search for code patterns, functions, classes, or text across the entire Unity project. Supports regex patterns and file type filtering.",
                input_schema = new ClaudeToolInputSchema
                {
                    properties = new Dictionary<string, ClaudeToolProperty>
                    {
                        ["query"] = new ClaudeToolProperty { type = "string", description = "Search query (text or regex pattern to find)" },
                        ["file_patterns"] = new ClaudeToolProperty { type = "string", description = "Comma-separated file patterns to search (e.g., '*.cs,*.js,*.shader') or 'all' for all text files" },
                        ["search_type"] = new ClaudeToolProperty { type = "string", description = "Search type: 'text' for simple text search, 'regex' for regex patterns, 'function' to find function definitions, 'class' to find class definitions" },
                        ["case_sensitive"] = new ClaudeToolProperty { type = "string", description = "Whether search should be case sensitive (true/false, default: false)" },
                        ["max_results"] = new ClaudeToolProperty { type = "string", description = "Maximum number of results to return (default: 50)" },
                        ["context_lines"] = new ClaudeToolProperty { type = "string", description = "Number of context lines to show around each match (default: 3)" }
                    },
                    required = new List<string> { "query" }
                }
            },
            new ClaudeTool
            {
                name = "find_references",
                description = "Find all references to a specific symbol (class, method, variable, etc.) across the codebase.",
                input_schema = new ClaudeToolInputSchema
                {
                    properties = new Dictionary<string, ClaudeToolProperty>
                    {
                        ["symbol"] = new ClaudeToolProperty { type = "string", description = "The symbol to find references for (e.g., 'ClassName', 'methodName', 'variableName')" },
                        ["file_patterns"] = new ClaudeToolProperty { type = "string", description = "File patterns to search in (default: '*.cs' for C# files)" },
                        ["include_declarations"] = new ClaudeToolProperty { type = "string", description = "Whether to include symbol declarations/definitions (true/false, default: true)" }
                    },
                    required = new List<string> { "symbol" }
                }
            }
        };
    }
    
    public static string ExecuteCodebaseSearchTool(ClaudeToolUse toolUse)
    {
        var inputDict = JsonConvert.DeserializeObject<Dictionary<string, object>>(
            JsonConvert.SerializeObject(toolUse.input));
        
        switch (toolUse.name)
        {
            case "search_codebase":
                Debug.Log("[ClaudeAI] CodebaseSearchTools: Searching codebase");
                return SearchCodebase(inputDict);
                
            case "find_references":
                Debug.Log("[ClaudeAI] CodebaseSearchTools: Finding references");
                return FindReferences(inputDict);
                
            default:
                return $"Unknown codebase search tool: {toolUse.name}";
        }
    }
    
    private static string SearchCodebase(Dictionary<string, object> input)
    {
        try
        {
            var query = input["query"].ToString();
            var filePatterns = input.ContainsKey("file_patterns") ? input["file_patterns"].ToString() : "*.cs,*.js,*.shader,*.hlsl,*.cginc";
            var searchType = input.ContainsKey("search_type") ? input["search_type"].ToString().ToLower() : "text";
            var caseSensitive = input.ContainsKey("case_sensitive") ? input["case_sensitive"].ToString().ToLower() == "true" : false;
            var maxResults = input.ContainsKey("max_results") ? int.Parse(input["max_results"].ToString()) : 50;
            var contextLines = input.ContainsKey("context_lines") ? int.Parse(input["context_lines"].ToString()) : 3;
            
            // Get search directory (entire project)
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            
            // Parse file patterns
            var patterns = filePatterns == "all" 
                ? new[] { "*.cs", "*.js", "*.ts", "*.shader", "*.hlsl", "*.cginc", "*.txt", "*.md", "*.json", "*.yaml", "*.yml" }
                : filePatterns.Split(',').Select(p => p.Trim()).ToArray();
            
            var results = new List<SearchResult>();
            
            foreach (var pattern in patterns)
            {
                var files = Directory.GetFiles(projectRoot, pattern, SearchOption.AllDirectories)
                    .Where(f => !IsIgnoredPath(f))
                    .Take(1000) // Limit files searched to prevent performance issues
                    .ToArray();
                
                foreach (var file in files)
                {
                    if (results.Count >= maxResults) break;
                    
                    try
                    {
                        var content = File.ReadAllText(file);
                        var matches = FindMatches(content, query, searchType, caseSensitive);
                        
                        foreach (var match in matches)
                        {
                            if (results.Count >= maxResults) break;
                            
                            var relativePath = Path.GetRelativePath(projectRoot, file);
                            var context = GetContext(content, match.LineNumber, contextLines);
                            
                            results.Add(new SearchResult
                            {
                                FilePath = relativePath,
                                LineNumber = match.LineNumber,
                                MatchText = match.Text,
                                Context = context
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"Error searching file {file}: {ex.Message}");
                    }
                }
            }
            
            return FormatSearchResults(query, results, searchType);
        }
        catch (Exception ex)
        {
            return $"❌ Failed to search codebase: {ex.Message}";
        }
    }
    
    private static string FindReferences(Dictionary<string, object> input)
    {
        try
        {
            var symbol = input["symbol"].ToString();
            var filePatterns = input.ContainsKey("file_patterns") ? input["file_patterns"].ToString() : "*.cs";
            var includeDeclarations = input.ContainsKey("include_declarations") ? input["include_declarations"].ToString().ToLower() == "true" : true;
            
            // Create regex patterns for finding references
            var patterns = new List<string>();
            
            // Method calls: symbol(
            patterns.Add($@"\b{Regex.Escape(symbol)}\s*\(");
            
            // Property/field access: .symbol or symbol.
            patterns.Add($@"\.{Regex.Escape(symbol)}\b");
            patterns.Add($@"\b{Regex.Escape(symbol)}\.");
            
            // Variable declarations and assignments
            patterns.Add($@"\b{Regex.Escape(symbol)}\s*[=:]");
            patterns.Add($@"[=:]\s*{Regex.Escape(symbol)}\b");
            
            // Class inheritance: : symbol
            patterns.Add($@":\s*{Regex.Escape(symbol)}\b");
            
            if (includeDeclarations)
            {
                // Class/method/property declarations
                patterns.Add($@"\b(class|interface|struct|enum)\s+{Regex.Escape(symbol)}\b");
                patterns.Add($@"\b(public|private|protected|internal)\s+.*\s+{Regex.Escape(symbol)}\s*[\(\{{]");
            }
            
            var searchInput = new Dictionary<string, object>
            {
                ["query"] = string.Join("|", patterns),
                ["file_patterns"] = filePatterns,
                ["search_type"] = "regex",
                ["case_sensitive"] = "true",
                ["max_results"] = "100",
                ["context_lines"] = "2"
            };
            
            var results = SearchCodebase(searchInput);
            return $"🔍 References for '{symbol}':\n\n{results}";
        }
        catch (Exception ex)
        {
            return $"❌ Failed to find references: {ex.Message}";
        }
    }
    
    private static List<MatchResult> FindMatches(string content, string query, string searchType, bool caseSensitive)
    {
        var matches = new List<MatchResult>();
        var lines = content.Split('\n');
        
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var lineToSearch = caseSensitive ? line : line.ToLower();
            var queryToSearch = caseSensitive ? query : query.ToLower();
            
            bool isMatch = false;
            
            switch (searchType)
            {
                case "text":
                    isMatch = lineToSearch.Contains(queryToSearch);
                    break;
                    
                case "regex":
                    try
                    {
                        var regexOptions = caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase;
                        isMatch = Regex.IsMatch(line, query, regexOptions);
                    }
                    catch (Exception)
                    {
                        // If regex is invalid, fall back to text search
                        isMatch = lineToSearch.Contains(queryToSearch);
                    }
                    break;
                    
                case "function":
                    var functionPattern = $@"\b(public|private|protected|internal)?\s*(static)?\s*\w+\s+{Regex.Escape(queryToSearch)}\s*\(";
                    var regexOpts = caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase;
                    isMatch = Regex.IsMatch(line, functionPattern, regexOpts);
                    break;
                    
                case "class":
                    var classPattern = $@"\b(public|private|protected|internal)?\s*(class|interface|struct|enum)\s+{Regex.Escape(queryToSearch)}\b";
                    var classRegexOpts = caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase;
                    isMatch = Regex.IsMatch(line, classPattern, classRegexOpts);
                    break;
            }
            
            if (isMatch)
            {
                matches.Add(new MatchResult
                {
                    LineNumber = i + 1,
                    Text = line.Trim()
                });
            }
        }
        
        return matches;
    }
    
    private static string GetContext(string content, int lineNumber, int contextLines)
    {
        var lines = content.Split('\n');
        var start = Math.Max(0, lineNumber - contextLines - 1);
        var end = Math.Min(lines.Length - 1, lineNumber + contextLines - 1);
        
        var context = new StringBuilder();
        for (int i = start; i <= end; i++)
        {
            var prefix = i == lineNumber - 1 ? ">>> " : "    ";
            context.AppendLine($"{prefix}{i + 1:D4}: {lines[i]}");
        }
        
        return context.ToString();
    }
    
    private static bool IsIgnoredPath(string path)
    {
        var ignoredDirs = new[] { "Library", "Temp", "Obj", "obj", "bin", "Logs", ".git", ".vs", ".vscode", "node_modules" };
        return ignoredDirs.Any(dir => path.Contains(Path.DirectorySeparatorChar + dir + Path.DirectorySeparatorChar));
    }
    
    private static string FormatSearchResults(string query, List<SearchResult> results, string searchType)
    {
        if (results.Count == 0)
        {
            return $"🔍 No matches found for '{query}' (search type: {searchType})";
        }
        
        var output = new StringBuilder();
        output.AppendLine($"🔍 Found {results.Count} matches for '{query}' (search type: {searchType}):");
        output.AppendLine();
        
        var groupedResults = results.GroupBy(r => r.FilePath).Take(20); // Limit to 20 files
        
        foreach (var fileGroup in groupedResults)
        {
            output.AppendLine($"📁 {fileGroup.Key}");
            
            foreach (var result in fileGroup.Take(5)) // Limit to 5 matches per file
            {
                output.AppendLine($"   📍 Line {result.LineNumber}: {result.MatchText}");
                if (!string.IsNullOrEmpty(result.Context))
                {
                    output.AppendLine(result.Context);
                }
                output.AppendLine();
            }
            
            if (fileGroup.Count() > 5)
            {
                output.AppendLine($"   ... and {fileGroup.Count() - 5} more matches in this file");
                output.AppendLine();
            }
        }
        
        if (results.GroupBy(r => r.FilePath).Count() > 20)
        {
            output.AppendLine($"... and matches in {results.GroupBy(r => r.FilePath).Count() - 20} more files");
        }
        
        return output.ToString().Trim();
    }
    
    private class MatchResult
    {
        public int LineNumber { get; set; }
        public string Text { get; set; }
    }
    
    private class SearchResult
    {
        public string FilePath { get; set; }
        public int LineNumber { get; set; }
        public string MatchText { get; set; }
        public string Context { get; set; }
    }
} 