namespace VpnHood.ResourceTranslator.Models;

public static class EngineModelSelector
{
    private const string DefaultModel = "gemini-2.5-flash-lite";
    private const string DefaultEngine = "gemini";

    public static (string engine, string model) SelectEngineAndModel(string? requestedEngine, string? requestedModel)
    {
        var engine = requestedEngine ?? DefaultEngine;
        var model = requestedModel ?? DefaultModel;

        // If engine is not explicitly set, auto-detect from model name
        if (string.IsNullOrWhiteSpace(requestedEngine) && !string.IsNullOrWhiteSpace(requestedModel))
        {
            engine = DetectEngineFromModel(requestedModel);
        }

        // Normalize engine names
        engine = NormalizeEngine(engine);

        // Use engine-specific default models when no model is specified
        if (string.IsNullOrWhiteSpace(requestedModel))
        {
            model = GetDefaultModelForEngine(engine);
        }

        return (engine, model);
    }

    private static string GetDefaultModelForEngine(string engine)
    {
        return engine.ToLowerInvariant() switch
        {
            "grok" => "grok-4-latest",
            "gpt" => "gpt-4o-mini",
            "gemini" => "gemini-2.5-flash-lite",
            "meta" => "llama-3.1-70b-instruct",
            _ => DefaultModel
        };
    }

    private static string DetectEngineFromModel(string model)
    {
        var modelLower = model.ToLowerInvariant();
        
        if (modelLower.Contains("gemini"))
            return "gemini";
        
        if (modelLower.Contains("llama") || modelLower.Contains("meta"))
            return "meta";
        
        if (modelLower.Contains("grok"))
            return "grok";
            
        // Default to chatgpt for all other models
        return "gpt";
    }

    private static string NormalizeEngine(string engine)
    {
        return engine.ToLowerInvariant() switch
        {
            "chatgpt" => "gpt",
            "openai" => "gpt",
            "meta-ai" => "meta",
            "metaai" => "meta",
            "grok-ai" => "grok",
            "grokai" => "grok",
            "x-ai" => "grok",
            "xai" => "grok",
            _ => engine.ToLowerInvariant()
        };
    }

    public static string GetEnvironmentVariableName(string engine)
    {
        return engine.ToLowerInvariant() switch
        {
            "gpt" or "chatgpt" => "OPENAI_API_KEY",
            "gemini" => "GEMINI_API_KEY",
            "meta" or "meta-ai" => "META_API_KEY",
            "grok" or "grok-ai" or "x-ai" => "GROK_API_KEY",
            _ => "GEMINI_API_KEY" // fallback to Gemini
        };
    }
}