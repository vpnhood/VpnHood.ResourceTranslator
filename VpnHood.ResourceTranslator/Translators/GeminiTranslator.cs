﻿using Mscc.GenerativeAI;
using System.Text;
using System.Text.Json;
using VpnHood.ResourceTranslator.Models;

namespace VpnHood.ResourceTranslator.Translators;

internal sealed class GeminiTranslator(
    string apiKey,
    string model)
    : ITranslator
{
    private readonly GoogleAI _googleAi = new(apiKey);

    public async Task<TranslateResult[]> TranslateAsync(PromptOptions promptOptions, CancellationToken cancellationToken)
    {
        var prompt = TranslateUtils.BuildPrompt(promptOptions);

        var geminiModel = _googleAi.GenerativeModel(model: model);
        var response = await geminiModel.GenerateContent(prompt, new GenerationConfig
        {
            ResponseMimeType = "application/json",
        }, cancellationToken: cancellationToken);

        if (response.Text == null)
            throw new Exception("AI result is null");

        return AiResponseParser.ParseResponse(response.Text);
    }
}