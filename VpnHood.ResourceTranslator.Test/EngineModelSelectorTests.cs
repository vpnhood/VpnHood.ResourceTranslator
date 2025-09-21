using VpnHood.ResourceTranslator.Models;

namespace VpnHood.ResourceTranslator.Test;

[TestClass]
public sealed class EngineModelSelectorTests
{
    [TestMethod]
    public void SelectEngineAndModel_AutoDetectsGemini()
    {
        // Arrange & Act
        var (engine, model) = EngineModelSelector.SelectEngineAndModel(null, "gemini-2.5-flash");

        // Assert
        Assert.AreEqual("gemini", engine);
        Assert.AreEqual("gemini-2.5-flash", model);
    }

    [TestMethod]
    public void SelectEngineAndModel_AutoDetectsGpt()
    {
        // Arrange & Act
        var (engine, model) = EngineModelSelector.SelectEngineAndModel(null, "gpt-4o-mini");

        // Assert
        Assert.AreEqual("gpt", engine);
        Assert.AreEqual("gpt-4o-mini", model);
    }

    [TestMethod]
    public void SelectEngineAndModel_AutoDetectsMeta()
    {
        // Arrange & Act
        var (engine, model) = EngineModelSelector.SelectEngineAndModel(null, "llama-3.1-70b-instruct");

        // Assert
        Assert.AreEqual("meta", engine);
        Assert.AreEqual("llama-3.1-70b-instruct", model);
    }

    [TestMethod]
    public void SelectEngineAndModel_AutoDetectsGrok()
    {
        // Arrange & Act
        var (engine, model) = EngineModelSelector.SelectEngineAndModel(null, "grok-beta");

        // Assert
        Assert.AreEqual("grok", engine);
        Assert.AreEqual("grok-beta", model);
    }

    [TestMethod]
    public void SelectEngineAndModel_ExplicitEngineOverridesDetection()
    {
        // Arrange & Act
        var (engine, model) = EngineModelSelector.SelectEngineAndModel("meta", "gpt-4");

        // Assert
        Assert.AreEqual("meta", engine);
        Assert.AreEqual("gpt-4", model);
    }

    [TestMethod]
    public void GetEnvironmentVariableName_ReturnsCorrectVariables()
    {
        Assert.AreEqual("GEMINI_API_KEY", EngineModelSelector.GetEnvironmentVariableName("gemini"));
        Assert.AreEqual("OPENAI_API_KEY", EngineModelSelector.GetEnvironmentVariableName("gpt"));
        Assert.AreEqual("META_API_KEY", EngineModelSelector.GetEnvironmentVariableName("meta"));
        Assert.AreEqual("GROK_API_KEY", EngineModelSelector.GetEnvironmentVariableName("grok"));
    }

    [TestMethod]
    public void SelectEngineAndModel_NormalizesEngineNames()
    {
        Assert.AreEqual("gpt", EngineModelSelector.SelectEngineAndModel("chatgpt", "test-model").engine);
        Assert.AreEqual("gpt", EngineModelSelector.SelectEngineAndModel("openai", "test-model").engine);
        Assert.AreEqual("meta", EngineModelSelector.SelectEngineAndModel("meta-ai", "test-model").engine);
        Assert.AreEqual("meta", EngineModelSelector.SelectEngineAndModel("metaai", "test-model").engine);
        Assert.AreEqual("grok", EngineModelSelector.SelectEngineAndModel("grok-ai", "test-model").engine);
        Assert.AreEqual("grok", EngineModelSelector.SelectEngineAndModel("x-ai", "test-model").engine);
    }
}