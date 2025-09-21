using System.Text.Json;
using OpenAI;
using OpenAI.Chat;
using VpnHood.ResourceTranslator.Models;

namespace VpnHood.ResourceTranslator.Translators;

internal sealed class ChatGptTranslator(
    string apiKey,
    string model)
    : ITranslator
{
    public async Task<TranslateResult[]> TranslateAsync(PromptOptions promptOptions, CancellationToken cancellationToken)
    {
        var prompt = TranslateUtils.BuildPrompt(promptOptions);

        var client = new OpenAIClient(apiKey);
        var chatClient = client.GetChatClient(model);

        var messages = new List<ChatMessage>
        {
            ChatMessage.CreateSystemMessage(
                "You are a professional localization engine. " +
                "Return ONLY a valid JSON array of translation objects. " +
                "Do not wrap the array in any additional objects or properties. " +
                "Do not include any commentary, explanations, or markdown formatting. " +
                "The response must start with '[' and end with ']'."),
            ChatMessage.CreateUserMessage(prompt),
        };

        var options = new ChatCompletionOptions
        {
            //ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat() // doesn't work as expected
        };

        var response = await chatClient.CompleteChatAsync(messages, options, cancellationToken);

        if (response?.Value?.Content == null || response.Value.Content.Count == 0)
            throw new Exception("AI result is null or empty");

        var content = response.Value.Content[0].Text;
        if (string.IsNullOrWhiteSpace(content))
            throw new Exception("AI result content is null or empty");

        return JsonSerializer.Deserialize<TranslateResult[]>(content)
               ?? throw new Exception("AI result deserialization failed");
    }
}
