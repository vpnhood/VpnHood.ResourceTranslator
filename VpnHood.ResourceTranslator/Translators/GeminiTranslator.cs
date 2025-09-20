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

    public async Task<TranslateResult[]> TranslateAsync(PromptOptions promptOptions, CancellationToken cancellationToken)
    {
        var prompt = BuildPrompt(promptOptions);

        var geminiModel = _googleAi.GenerativeModel(model: model);
        var response = await geminiModel.GenerateContent(prompt, new GenerationConfig
        {
            ResponseMimeType = "application/json",
        }, cancellationToken: cancellationToken);

        if (response.Text == null)
            throw new Exception("AI result is null");

        return JsonSerializer.Deserialize<TranslateResult[]>(response.Text)
            ?? throw new Exception("AI result deserialization failed");
    }

    private static string BuildPrompt(PromptOptions options)
    {
        var sb = new StringBuilder();
        sb.AppendLine(options.Prompt);
        sb.AppendLine();
        var sample = new TranslateResult[] {
            new()
            {
                SourceLanguage = "en",
                TargetLanguage = "fr",
                Key = "Key1",
                Text = "SourceText1",
                TranslatedText = "TranslatedText1"
            },
            new()
            {
                SourceLanguage = "en",
                TargetLanguage = "it",
                Key = "Key2",
                Text = "SourceText2",
                TranslatedText = "TranslatedText2"
            }
        };
        sb.AppendLine("The output format is json array.: " + JsonSerializer.Serialize(sample));
        
        sb.AppendLine();
        sb.AppendLine("The items need to be translated:");
        sb.AppendLine($"Source language: {JsonSerializer.Serialize(options.Items)}");
        return sb.ToString();
    }
}