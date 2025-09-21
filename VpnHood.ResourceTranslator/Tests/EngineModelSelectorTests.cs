using VpnHood.ResourceTranslator.Models;
using Xunit;

namespace VpnHood.ResourceTranslator.Tests;

public class EngineModelSelectorTests
{
    [Fact]
    public void SelectEngineAndModel_AutoDetectsGemini()
    {
        // Arrange & Act
        var (engine, model) = EngineModelSelector.SelectEngineAndModel(null, "gemini-2.5-flash");

        // Assert
        Assert.Equal("gemini", engine);
        Assert.Equal("gemini-2.5-flash", model);
    }

    [Fact]
    public void SelectEngineAndModel_AutoDetectsGpt()
    {
        // Arrange & Act
        var (engine, model) = EngineModelSelector.SelectEngineAndModel(null, "gpt-4o-mini");

        // Assert
        Assert.Equal("gpt", engine);
        Assert.Equal("gpt-4o-mini", model);
    }

    [Fact]
    public void SelectEngineAndModel_AutoDetectsMeta()
    {
        // Arrange & Act
        var (engine, model) = EngineModelSelector.SelectEngineAndModel(null, "llama-3.1-70b-instruct");

        // Assert
        Assert.Equal("meta", engine);
        Assert.Equal("llama-3.1-70b-instruct", model);
    }

    [Fact]
    public void SelectEngineAndModel_AutoDetectsGrok()
    {
        // Arrange & Act
        var (engine, model) = EngineModelSelector.SelectEngineAndModel(null, "grok-4-latest");

        // Assert
        Assert.Equal("grok", engine);
        Assert.Equal("grok-4-latest", model);
    }

    [Fact]
    public void SelectEngineAndModel_ExplicitEngineOverridesDetection()
    {
        // Arrange & Act
        var (engine, model) = EngineModelSelector.SelectEngineAndModel("meta", "gpt-4");

        // Assert
        Assert.Equal("meta", engine);
        Assert.Equal("gpt-4", model);
    }

    [Fact]
    public void GetEnvironmentVariableName_ReturnsCorrectVariables()
    {
        Assert.Equal("GEMINI_API_KEY", EngineModelSelector.GetEnvironmentVariableName("gemini"));
        Assert.Equal("OPENAI_API_KEY", EngineModelSelector.GetEnvironmentVariableName("gpt"));
        Assert.Equal("META_API_KEY", EngineModelSelector.GetEnvironmentVariableName("meta"));
        Assert.Equal("GROK_API_KEY", EngineModelSelector.GetEnvironmentVariableName("grok"));
    }

    [Fact]
    public void SelectEngineAndModel_NormalizesEngineNames()
    {
        Assert.Equal("gpt", EngineModelSelector.SelectEngineAndModel("chatgpt", "test-model").engine);
        Assert.Equal("gpt", EngineModelSelector.SelectEngineAndModel("openai", "test-model").engine);
        Assert.Equal("meta", EngineModelSelector.SelectEngineAndModel("meta-ai", "test-model").engine);
        Assert.Equal("meta", EngineModelSelector.SelectEngineAndModel("metaai", "test-model").engine);
        Assert.Equal("grok", EngineModelSelector.SelectEngineAndModel("grok-ai", "test-model").engine);
        Assert.Equal("grok", EngineModelSelector.SelectEngineAndModel("x-ai", "test-model").engine);
    }
}