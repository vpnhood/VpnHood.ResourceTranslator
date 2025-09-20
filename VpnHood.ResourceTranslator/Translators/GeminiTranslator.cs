using Mscc.GenerativeAI;
using System.Text;
using System.Text.Json;
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

        if (response.Text == null)
            throw new Exception("AI result is null");

        return JsonSerializer.Deserialize<TranslateResult>(response.Text)?.TranslatedText
            ?? throw new Exception("AI result deserialization failed");
    }

    private static string BuildPrompt(PromptOptions options)
    {
        var template = options.Prompt;

        var sb = new StringBuilder();
        sb.AppendLine(template);
        sb.AppendLine();
        sb.AppendLine("The output json format is: " + JsonSerializer.Serialize(new TranslateResult
        {
            Text = "SourceText",
            TranslatedText = "TranslatedText"
        }));

        sb.AppendLine();
        sb.AppendLine($"Source language: {options.SourceLanguage}");
        sb.AppendLine($"Target language: {options.TargetLanguage}");
        sb.AppendLine("Text:");
        sb.AppendLine(options.Text);

        return sb.ToString();
    }
}