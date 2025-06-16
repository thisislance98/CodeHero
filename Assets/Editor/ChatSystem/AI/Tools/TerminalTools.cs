using UnityEngine;
using System.Collections.Generic;
using System.Text;
using System;
using Newtonsoft.Json;
using System.IO;
using System.Diagnostics;
using System.Threading.Tasks;

public static class TerminalTools
{
    public static List<ClaudeTool> GetTerminalTools()
    {
        return new List<ClaudeTool>
        {
            new ClaudeTool
            {
                name = "execute_terminal_command",
                description = "Execute terminal/command line commands. Useful for git operations, package management, build scripts, and other system-level tasks.",
                input_schema = new ClaudeToolInputSchema
                {
                    properties = new Dictionary<string, ClaudeToolProperty>
                    {
                        ["command"] = new ClaudeToolProperty { type = "string", description = "The command to execute (e.g., 'git status', 'npm install', 'ls -la')" },
                        ["working_directory"] = new ClaudeToolProperty { type = "string", description = "Working directory for command execution (default: project root)" },
                        ["timeout_seconds"] = new ClaudeToolProperty { type = "string", description = "Timeout in seconds for command execution (default: 30)" },
                        ["capture_output"] = new ClaudeToolProperty { type = "string", description = "Whether to capture command output (true/false, default: true)" }
                    },
                    required = new List<string> { "command" }
                }
            }
        };
    }
    
    public static string ExecuteTerminalTool(ClaudeToolUse toolUse)
    {
        var inputDict = JsonConvert.DeserializeObject<Dictionary<string, object>>(
            JsonConvert.SerializeObject(toolUse.input));
        
        switch (toolUse.name)
        {
            case "execute_terminal_command":
                UnityEngine.Debug.Log("[ClaudeAI] TerminalTools: Executing terminal command");
                return ExecuteTerminalCommand(inputDict);
                
            default:
                return $"Unknown terminal tool: {toolUse.name}";
        }
    }
    
    private static string ExecuteTerminalCommand(Dictionary<string, object> input)
    {
        try
        {
            var command = input["command"].ToString();
            var workingDir = input.ContainsKey("working_directory") ? input["working_directory"].ToString() : GetProjectRoot();
            var timeoutSeconds = input.ContainsKey("timeout_seconds") ? int.Parse(input["timeout_seconds"].ToString()) : 30;
            var captureOutput = input.ContainsKey("capture_output") ? input["capture_output"].ToString().ToLower() == "true" : true;
            
            // Validate working directory
            if (!Directory.Exists(workingDir))
            {
                return $"Error: Working directory does not exist: {workingDir}";
            }
            
            var result = new StringBuilder();
            result.AppendLine($"🖥️ Executing: {command}");
            result.AppendLine($"📁 Working Directory: {workingDir}");
            result.AppendLine($"⏱️ Timeout: {timeoutSeconds}s");
            result.AppendLine();
            
            // Determine shell and command based on platform
            string fileName;
            string arguments;
            
            if (Application.platform == RuntimePlatform.WindowsEditor)
            {
                fileName = "cmd.exe";
                arguments = $"/c {command}";
            }
            else
            {
                fileName = "/bin/bash";
                arguments = $"-c \"{command}\"";
            }
            
            var processInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                WorkingDirectory = workingDir,
                UseShellExecute = false,
                RedirectStandardOutput = captureOutput,
                RedirectStandardError = captureOutput,
                CreateNoWindow = true
            };
            
            using (var process = new Process { StartInfo = processInfo })
            {
                var output = new StringBuilder();
                var error = new StringBuilder();
                
                if (captureOutput)
                {
                    process.OutputDataReceived += (sender, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                            output.AppendLine(e.Data);
                    };
                    
                    process.ErrorDataReceived += (sender, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                            error.AppendLine(e.Data);
                    };
                }
                
                process.Start();
                
                if (captureOutput)
                {
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                }
                
                bool finished = process.WaitForExit(timeoutSeconds * 1000);
                
                if (!finished)
                {
                    process.Kill();
                    return result.ToString() + "❌ Command timed out and was terminated.";
                }
                
                result.AppendLine($"✅ Exit Code: {process.ExitCode}");
                result.AppendLine();
                
                if (captureOutput)
                {
                    if (output.Length > 0)
                    {
                        result.AppendLine("📤 Standard Output:");
                        result.AppendLine(output.ToString());
                    }
                    
                    if (error.Length > 0)
                    {
                        result.AppendLine("❌ Standard Error:");
                        result.AppendLine(error.ToString());
                    }
                }
                
                if (process.ExitCode != 0)
                {
                    result.AppendLine($"⚠️ Command exited with non-zero code: {process.ExitCode}");
                }
                
                return result.ToString().Trim();
            }
        }
        catch (Exception ex)
        {
            return $"❌ Failed to execute terminal command: {ex.Message}";
        }
    }
    
    private static string GetProjectRoot()
    {
        // Get project root (parent of Assets folder)
        var assetsPath = Application.dataPath;
        return Directory.GetParent(assetsPath)?.FullName ?? assetsPath;
    }
} 