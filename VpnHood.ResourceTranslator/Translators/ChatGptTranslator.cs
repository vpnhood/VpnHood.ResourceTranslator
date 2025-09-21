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
                "Return must be a JSON array even for single object matching the requested schema. No extra commentary. " +
                "the response should not start with single json object. it must be array"),
            ChatMessage.CreateUserMessage(prompt),
        };

        var options = new ChatCompletionOptions
        {
            ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat()
        };

        var response = await chatClient.CompleteChatAsync(messages, options, cancellationToken);

        if (response?.Value?.Content == null || response.Value.Content.Count == 0)
            throw new Exception("AI result is null or empty");

        var content = response.Value.Content[0].Text;
        if (string.IsNullOrWhiteSpace(content))
            throw new Exception("AI result content is null or empty");

        if (content.TrimStart()[0] == '{')
        {
            var result = JsonSerializer.Deserialize<TranslateResult>(content) 
                         ?? throw new Exception("AI result deserialization failed");
            return new[] { result };

        }
        else
        {
            var result = JsonSerializer.Deserialize<TranslateResult[]>(content)
                   ?? throw new Exception("AI result deserialization failed");
            return result;
        }
    }
}
