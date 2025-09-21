namespace VpnHood.ResourceTranslator.Models;

internal static class EngineModelSelector
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

        return (engine, model);
    }

    private static string DetectEngineFromModel(string model)
    {
        var modelLower = model.ToLowerInvariant();
        
        if (modelLower.Contains("gemini"))
            return "gemini";
            
        // Default to chatgpt for all other models
        return "gpt";
    }

    private static string NormalizeEngine(string engine)
    {
        return engine.ToLowerInvariant() switch
        {
            "chatgpt" => "gpt",
            "openai" => "gpt",
            _ => engine.ToLowerInvariant()
        };
    }

    public static string GetEnvironmentVariableName(string engine)
    {
        return engine.ToLowerInvariant() switch
        {
            "gpt" or "chatgpt" => "OPENAI_API_KEY",
            "gemini" => "GEMINI_API_KEY",
            _ => "GEMINI_API_KEY" // fallback to Gemini
        };
    }
}