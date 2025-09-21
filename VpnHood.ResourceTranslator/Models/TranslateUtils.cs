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