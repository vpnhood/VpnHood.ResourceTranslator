namespace VpnHood.ResourceTranslator.Translators;

internal interface ITranslator
{
    Task<string> TranslateAsync(string text, string sourceLang, string targetLang, string? extraPrompt, CancellationToken cancellationToken);
}