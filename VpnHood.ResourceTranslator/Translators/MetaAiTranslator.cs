using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using VpnHood.ResourceTranslator.Models;

namespace VpnHood.ResourceTranslator.Translators;

internal sealed class MetaAiTranslator(
    string apiKey,
    string model)
    : ITranslator
{
    private static readonly HttpClient HttpClient = new();

    public async Task<TranslateResult[]> TranslateAsync(PromptOptions promptOptions, CancellationToken cancellationToken)
    {
        var prompt = TranslateUtils.BuildPrompt(promptOptions);

        var requestBody = new
        {
            model = model,
            messages = new[]
            {
                new
                {
                    role = "system",
                    content = TranslateUtils.BuildSystemPrompt()
                },
                new
                {
                    role = "user",
                    content = prompt
                }
            },
            max_tokens = 4000,
            temperature = 0.1,
            response_format = new { type = "json_object" }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.meta.ai/v1/chat/completions");
        request.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        using var response = await HttpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new Exception($"Meta AI API error ({response.StatusCode}): {errorContent}");
        }

        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
        using var document = JsonDocument.Parse(responseContent);

        var choices = document.RootElement.GetProperty("choices");
        if (choices.GetArrayLength() == 0)
            throw new Exception("Meta AI response contains no choices");

        var message = choices[0].GetProperty("message");
        var content = message.GetProperty("content").GetString();

        if (string.IsNullOrWhiteSpace(content))
            throw new Exception("Meta AI result content is null or empty");

        // Clean up the content to extract just the JSON array
        content = content.Trim();
        if (content.StartsWith("```json"))
        {
            var startIndex = content.IndexOf('[');
            var endIndex = content.LastIndexOf(']');
            if (startIndex >= 0 && endIndex > startIndex)
            {
                content = content.Substring(startIndex, endIndex - startIndex + 1);
            }
        }

        return JsonSerializer.Deserialize<TranslateResult[]>(content)
               ?? throw new Exception("Meta AI result deserialization failed");
    }
}