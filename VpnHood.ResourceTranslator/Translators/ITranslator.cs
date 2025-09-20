using VpnHood.ResourceTranslator.Models;

namespace VpnHood.ResourceTranslator.Translators;

internal interface ITranslator
{
    Task<string> TranslateAsync(PromptOptions promptOptions, CancellationToken cancellationToken);
}