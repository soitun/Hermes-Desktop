using Hermes.Agent.LLM;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HermesDesktop.Tests.LLM;

[TestClass]
public class ModelCatalogTests
{
    [TestMethod]
    public void Providers_LmStudioIncluded()
    {
        CollectionAssert.Contains(ModelCatalog.Providers.ToArray(), "lmstudio");
    }

    [TestMethod]
    public void GetDefaultBaseUrl_LmStudioProvider_ReturnsLmStudioServerUrl()
    {
        Assert.AreEqual("http://localhost:1234/v1", ModelCatalog.GetDefaultBaseUrl("lmstudio"));
    }

    [TestMethod]
    public void NormalizeProvider_CustomAlias_ReturnsLocalProvider()
    {
        Assert.AreEqual("local", ModelCatalog.NormalizeProvider("custom"));
    }

    [TestMethod]
    public void GetDefaultModelId_LmStudioProvider_ReturnsCustomModelId()
    {
        Assert.AreEqual("custom", ModelCatalog.GetDefaultModelId("lmstudio"));
    }
}
