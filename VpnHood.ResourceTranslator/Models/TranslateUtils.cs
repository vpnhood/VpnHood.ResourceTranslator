using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using VpnHood.ResourceTranslator.Translators;

namespace VpnHood.ResourceTranslator.Models;

public static class TranslateUtils
{
    public static string BuildPrompt(PromptOptions options)
    {
        var sb = new StringBuilder();
        sb.AppendLine(options.Prompt);
        sb.AppendLine();
        
        // Create a more explicit example for ChatGPT
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
        
        sb.AppendLine("IMPORTANT: Return ONLY a JSON array (starting with '[' and ending with ']'). Do not wrap it in any other object.");
        sb.AppendLine("Expected output format:");
        sb.AppendLine(JsonSerializer.Serialize(sample, new JsonSerializerOptions { WriteIndented = true }));
        sb.AppendLine();
        sb.AppendLine("Items to translate:");
        sb.AppendLine(JsonSerializer.Serialize(options.Items, new JsonSerializerOptions { WriteIndented = true }));
        
        return sb.ToString();
    }
}