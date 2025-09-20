namespace VpnHood.ResourceTranslator.Models;

public class PromptOptions
{
    public string SourceLanguage { get; set; } = string.Empty;
    public string TargetLanguage { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string? Prompt { get; set; }
}