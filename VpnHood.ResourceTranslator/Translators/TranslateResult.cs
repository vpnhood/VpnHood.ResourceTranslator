namespace VpnHood.ResourceTranslator.Translators;

public class TranslateResult
{
    public required string Text { get; set; } 
    public required string TranslatedText { get; set; }
    public required string SourceLanguage { get; set; }
    public required string TargetLanguage { get; set; }
    public required string Key { get; set; }
}