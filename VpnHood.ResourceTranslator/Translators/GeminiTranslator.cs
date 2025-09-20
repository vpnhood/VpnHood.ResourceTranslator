using System.Text;
using System.Text.Json;
using VpnHood.ResourceTranslator.Models;

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

    public async Task<string> TranslateAsync(PromptOptions promptOptions, CancellationToken cancellationToken)
    {
        var prompt = BuildPromptAsync(promptOptions);

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

    private static string BuildPromptAsync(PromptOptions options)
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