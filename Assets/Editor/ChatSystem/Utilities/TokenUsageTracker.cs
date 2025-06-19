using UnityEngine;
using System;

[System.Serializable]
public class TokenUsageData
{
    public int inputTokens;
    public int outputTokens;
    public string modelName;
    public DateTime timestamp;
    public double cost;
    
    public TokenUsageData(int inputTokens, int outputTokens, string modelName)
    {
        this.inputTokens = inputTokens;
        this.outputTokens = outputTokens;
        this.modelName = modelName;
        this.timestamp = DateTime.Now;
        this.cost = CalculateCost(inputTokens, outputTokens, modelName);
    }
    
    public static double CalculateCost(int inputTokens, int outputTokens, string modelName)
    {
        // Claude Sonnet 4 pricing: $3 per million input tokens, $15 per million output tokens
        double inputCostPerMillion = 3.0;
        double outputCostPerMillion = 15.0;
        
        // Adjust pricing based on model (future-proof for other Claude models)
        if (modelName.Contains("opus"))
        {
            inputCostPerMillion = 15.0;
            outputCostPerMillion = 75.0;
        }
        else if (modelName.Contains("haiku"))
        {
            inputCostPerMillion = 0.8;
            outputCostPerMillion = 4.0;
        }
        // Default to Sonnet 4 pricing for sonnet models
        
        double inputCost = (inputTokens / 1000000.0) * inputCostPerMillion;
        double outputCost = (outputTokens / 1000000.0) * outputCostPerMillion;
        
        return inputCost + outputCost;
    }
    
    public string GetFormattedCost()
    {
        if (cost < 0.001)
        {
            return $"${cost:F6}";
        }
        else if (cost < 0.01)
        {
            return $"${cost:F4}";
        }
        else
        {
            return $"${cost:F2}";
        }
    }
    
    public string GetUsageSummary()
    {
        return $"Tokens: {inputTokens:N0} in, {outputTokens:N0} out | Cost: {GetFormattedCost()}";
    }
    
    public string GetDetailedUsage()
    {
        return $"Input: {inputTokens:N0} tokens, Output: {outputTokens:N0} tokens\n" +
               $"Model: {modelName}\n" +
               $"Cost: {GetFormattedCost()}\n" +
               $"Time: {timestamp:HH:mm:ss}";
    }
}

public static class TokenUsageTracker
{
    public static TokenUsageData TrackUsage(ClaudeUsage usage, string modelName)
    {
        if (usage == null)
        {
            Debug.LogWarning("[TokenUsageTracker] No usage data available from Claude API response");
            return new TokenUsageData(0, 0, modelName);
        }
        
        return new TokenUsageData(usage.input_tokens, usage.output_tokens, modelName);
    }
    
    public static string FormatTokenCount(int tokens)
    {
        if (tokens >= 1000000)
        {
            return $"{tokens / 1000000.0:F1}M";
        }
        else if (tokens >= 1000)
        {
            return $"{tokens / 1000.0:F1}K";
        }
        else
        {
            return tokens.ToString();
        }
    }
} 