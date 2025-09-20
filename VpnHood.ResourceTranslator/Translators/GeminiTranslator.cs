using System.Text;
using System.Text.Json;

namespace VpnHood.ResourceTranslator.Translators;

internal sealed class GeminiTranslator(
    string apiKey, 
    string model) 
    : ITranslator
{
    private readonly HttpClient _http = new()
    {
        BaseAddress = new Uri("https://generativelanguage.googleapis.com/")
    };

    public async Task<string> TranslateAsync(string text, string sourceLang, string targetLang, string? extraPrompt, CancellationToken cancellationToken)
    {
        var prompt = BuildPrompt(text, sourceLang, targetLang, extraPrompt);

        var body = new
        {
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = new[] { new { text = prompt } }
                }
            }
        };

        var url = $"v1beta/models/{model}:generateContent?key={apiKey}";
        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        req.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        using var resp = await _http.SendAsync(req, cancellationToken);
        var payload = await resp.Content.ReadAsStringAsync(cancellationToken);
        if (!resp.IsSuccessStatusCode)
        {
            throw new Exception($"Gemini API error {(int)resp.StatusCode}: {payload}");
        }

        using var doc = JsonDocument.Parse(payload);
        var root = doc.RootElement;
        var candidate = root.GetProperty("candidates")[0];
        var content = candidate.GetProperty("content");
        var parts = content.GetProperty("parts");
        var txt = parts[0].GetProperty("text").GetString();
        return txt ?? string.Empty;
    }

    private static string BuildPrompt(string text, string sourceLang, string targetLang, string? extra)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are an expert app localization system.");
        sb.AppendLine("Task: Translate the given string from the source language to the target language.");
        sb.AppendLine("Rules:");
        sb.AppendLine("- Preserve placeholders exactly as-is: tokens in curly braces like {x}, {name}, {minutes}.");
        sb.AppendLine("- Preserve HTML tags, entities, and attributes exactly (do not translate tags or attribute names).");
        sb.AppendLine("- Preserve punctuation, Markdown, and capitalization style when appropriate.");
        sb.AppendLine("- Keep URLs and domains unchanged.");
        sb.AppendLine("- Return ONLY the translated string without quotes or extra commentary.");
        sb.AppendLine("- If the text is already a URL or looks untranslatable, return it unchanged.");
        if (!string.IsNullOrWhiteSpace(extra))
        {
            sb.AppendLine();
            sb.AppendLine("Additional guidelines:");
            sb.AppendLine(extra);
        }
        sb.AppendLine();
        sb.AppendLine($"Source language: {sourceLang}");
        sb.AppendLine($"Target language: {targetLang}");
        sb.AppendLine("Text:");
        sb.AppendLine(text);
        return sb.ToString();
    }
}