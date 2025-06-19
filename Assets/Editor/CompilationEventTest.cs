using UnityEngine;
using UnityEditor;
using UnityEditor.Compilation;
using System;
using System.IO;

[InitializeOnLoad]
public static class CompilationEventTest
{
    private static bool eventsSetup = false;
    
    // Use SessionState to persist data across domain reloads
    private const string COMPILATION_START_COUNT_KEY = "CompilationEventTest_StartCount";
    private const string COMPILATION_FINISH_COUNT_KEY = "CompilationEventTest_FinishCount";
    private const string ASSEMBLY_START_COUNT_KEY = "CompilationEventTest_AssemblyStartCount";
    private const string ASSEMBLY_FINISH_COUNT_KEY = "CompilationEventTest_AssemblyFinishCount";
    private const string LAST_START_TIME_KEY = "CompilationEventTest_LastStartTime";
    private const string LAST_FINISH_TIME_KEY = "CompilationEventTest_LastFinishTime";
    
    private static int CompilationStartCount
    {
        get => SessionState.GetInt(COMPILATION_START_COUNT_KEY, 0);
        set => SessionState.SetInt(COMPILATION_START_COUNT_KEY, value);
    }
    
    private static int CompilationFinishCount
    {
        get => SessionState.GetInt(COMPILATION_FINISH_COUNT_KEY, 0);
        set => SessionState.SetInt(COMPILATION_FINISH_COUNT_KEY, value);
    }
    
    private static int AssemblyCompilationStartCount
    {
        get => SessionState.GetInt(ASSEMBLY_START_COUNT_KEY, 0);
        set => SessionState.SetInt(ASSEMBLY_START_COUNT_KEY, value);
    }
    
    private static int AssemblyCompilationFinishCount
    {
        get => SessionState.GetInt(ASSEMBLY_FINISH_COUNT_KEY, 0);
        set => SessionState.SetInt(ASSEMBLY_FINISH_COUNT_KEY, value);
    }
    
    private static DateTime LastCompilationStart
    {
        get
        {
            var ticks = SessionState.GetString(LAST_START_TIME_KEY, "");
            return string.IsNullOrEmpty(ticks) ? DateTime.MinValue : new DateTime(long.Parse(ticks));
        }
        set => SessionState.SetString(LAST_START_TIME_KEY, value.Ticks.ToString());
    }
    
    private static DateTime LastCompilationFinish
    {
        get
        {
            var ticks = SessionState.GetString(LAST_FINISH_TIME_KEY, "");
            return string.IsNullOrEmpty(ticks) ? DateTime.MinValue : new DateTime(long.Parse(ticks));
        }
        set => SessionState.SetString(LAST_FINISH_TIME_KEY, value.Ticks.ToString());
    }
    
    static CompilationEventTest()
    {
        SetupCompilationEvents();
        Debug.Log("[CompilationEventTest] Static constructor called - events setup");
    }
    
    private static void SetupCompilationEvents()
    {
        if (!eventsSetup)
        {
            // Global compilation events
            CompilationPipeline.compilationStarted -= OnCompilationStarted;
            CompilationPipeline.compilationFinished -= OnCompilationFinished;
            CompilationPipeline.compilationStarted += OnCompilationStarted;
            CompilationPipeline.compilationFinished += OnCompilationFinished;
            
            // Assembly-level compilation events
            CompilationPipeline.assemblyCompilationStarted -= OnAssemblyCompilationStarted;
            CompilationPipeline.assemblyCompilationFinished -= OnAssemblyCompilationFinished;
            CompilationPipeline.assemblyCompilationStarted += OnAssemblyCompilationStarted;
            CompilationPipeline.assemblyCompilationFinished += OnAssemblyCompilationFinished;
            
            eventsSetup = true;
            Debug.Log("[CompilationEventTest] Compilation events subscribed successfully");
        }
    }
    
    private static void OnCompilationStarted(object context)
    {
        LastCompilationStart = DateTime.Now;
        CompilationStartCount++;
        Debug.Log($"[CompilationEventTest] *** COMPILATION STARTED *** (#{CompilationStartCount}) at {LastCompilationStart:HH:mm:ss.fff}");
        Debug.Log($"[CompilationEventTest] Context: {context?.GetType()?.Name ?? "null"}");
        Debug.Log($"[CompilationEventTest] EditorApplication.isCompiling: {EditorApplication.isCompiling}");
    }
    
    private static void OnCompilationFinished(object context)
    {
        LastCompilationFinish = DateTime.Now;
        CompilationFinishCount++;
        var duration = LastCompilationFinish - LastCompilationStart;
        Debug.Log($"[CompilationEventTest] *** COMPILATION FINISHED *** (#{CompilationFinishCount}) at {LastCompilationFinish:HH:mm:ss.fff}");
        Debug.Log($"[CompilationEventTest] Duration: {duration.TotalMilliseconds:F0}ms");
        Debug.Log($"[CompilationEventTest] Context: {context?.GetType()?.Name ?? "null"}");
        Debug.Log($"[CompilationEventTest] EditorApplication.isCompiling: {EditorApplication.isCompiling}");
    }
    
    private static void OnAssemblyCompilationStarted(string assemblyPath)
    {
        AssemblyCompilationStartCount++;
        Debug.Log($"[CompilationEventTest] Assembly compilation started (#{AssemblyCompilationStartCount}): {Path.GetFileName(assemblyPath)}");
    }
    
    private static void OnAssemblyCompilationFinished(string assemblyPath, CompilerMessage[] messages)
    {
        AssemblyCompilationFinishCount++;
        var errors = 0;
        var warnings = 0;
        foreach (var msg in messages)
        {
            if (msg.type == CompilerMessageType.Error) errors++;
            else if (msg.type == CompilerMessageType.Warning) warnings++;
        }
        Debug.Log($"[CompilationEventTest] Assembly compilation finished (#{AssemblyCompilationFinishCount}): {Path.GetFileName(assemblyPath)} - {errors} errors, {warnings} warnings");
    }
    
    [MenuItem("Tools/Compilation Test/Trigger Manual Compilation")]
    public static void TriggerManualCompilation()
    {
        Debug.Log("[CompilationEventTest] === MANUALLY TRIGGERING COMPILATION ===");
        Debug.Log($"[CompilationEventTest] Before: EditorApplication.isCompiling = {EditorApplication.isCompiling}");
        
        AssetDatabase.Refresh();
        CompilationPipeline.RequestScriptCompilation();
        
        EditorApplication.delayCall += () =>
        {
            Debug.Log($"[CompilationEventTest] After request: EditorApplication.isCompiling = {EditorApplication.isCompiling}");
        };
    }
    
    [MenuItem("Tools/Compilation Test/Create Test Script")]
    public static void CreateTestScript()
    {
        var scriptName = $"TestScript_{DateTime.Now:HHmmss}";
        var scriptPath = Path.Combine(Application.dataPath, "Scripts", $"{scriptName}.cs");
        
        var scriptContent = $@"using UnityEngine;

public class {scriptName} : MonoBehaviour
{{
    void Start()
    {{
        Debug.Log(""Hello from {scriptName}!"");
    }}
}}";
        
        Debug.Log($"[CompilationEventTest] === CREATING TEST SCRIPT: {scriptName} ===");
        
        // Ensure Scripts directory exists
        var scriptsDir = Path.Combine(Application.dataPath, "Scripts");
        if (!Directory.Exists(scriptsDir))
        {
            Directory.CreateDirectory(scriptsDir);
        }
        
        File.WriteAllText(scriptPath, scriptContent);
        AssetDatabase.Refresh();
        
        Debug.Log($"[CompilationEventTest] Test script created at: {scriptPath}");
    }
    
    [MenuItem("Tools/Compilation Test/Show Event Statistics")]
    public static void ShowEventStatistics()
    {
        Debug.Log("=== COMPILATION EVENT STATISTICS ===");
        Debug.Log($"Events setup: {eventsSetup}");
        Debug.Log($"Compilation started events: {CompilationStartCount}");
        Debug.Log($"Compilation finished events: {CompilationFinishCount}");
        Debug.Log($"Assembly compilation started events: {AssemblyCompilationStartCount}");
        Debug.Log($"Assembly compilation finished events: {AssemblyCompilationFinishCount}");
        Debug.Log($"Last compilation start: {(CompilationStartCount > 0 ? LastCompilationStart.ToString("HH:mm:ss.fff") : "Never")}");
        Debug.Log($"Last compilation finish: {(CompilationFinishCount > 0 ? LastCompilationFinish.ToString("HH:mm:ss.fff") : "Never")}");
        Debug.Log($"Current EditorApplication.isCompiling: {EditorApplication.isCompiling}");
    }
    
    [MenuItem("Tools/Compilation Test/Reset Statistics")]
    public static void ResetStatistics()
    {
        CompilationStartCount = 0;
        CompilationFinishCount = 0;
        AssemblyCompilationStartCount = 0;
        AssemblyCompilationFinishCount = 0;
        LastCompilationStart = DateTime.MinValue;
        LastCompilationFinish = DateTime.MinValue;
        Debug.Log("[CompilationEventTest] Statistics reset");
    }
} 