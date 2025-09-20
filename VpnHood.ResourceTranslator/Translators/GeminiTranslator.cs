using Mscc.GenerativeAI;
using System.Text;
using VpnHood.ResourceTranslator.Models;

namespace VpnHood.ResourceTranslator.Translators;

internal sealed class GeminiTranslator(
    string apiKey, 
    string model) 
    : ITranslator
{
    private readonly GoogleAI _googleAi = new(apiKey);

    public async Task<string> TranslateAsync(PromptOptions promptOptions, CancellationToken cancellationToken)
    {
        var prompt = BuildPrompt(promptOptions);
        
        var geminiModel = _googleAi.GenerativeModel(model: model);
        var response = await geminiModel.GenerateContent(prompt, new GenerationConfig
        {
            ResponseMimeType = "application/json",
        }, cancellationToken: cancellationToken);
        
        return response?.Text ?? string.Empty;
    }

    private static string BuildPrompt(PromptOptions options)
    {
        var template = options.Prompt;
        
        var sb = new StringBuilder();
        sb.AppendLine(template);
        
        sb.AppendLine();
        sb.AppendLine($"Source language: {options.SourceLanguage}");
        sb.AppendLine($"Target language: {options.TargetLanguage}");
        sb.AppendLine("Text:");
        sb.AppendLine(options.Text);
        
        return sb.ToString();
    }
}