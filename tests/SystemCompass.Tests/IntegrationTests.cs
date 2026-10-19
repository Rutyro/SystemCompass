using Microsoft.VisualStudio.TestTools.UnitTesting;
using SystemCompass.Core.Startup;
using SystemCompass.Windows;

namespace SystemCompass.Tests;

[TestClass]
public sealed class IntegrationTests
{
    [TestMethod]
    [TestCategory("Integration")]
    public void RealProvidersEnumerateWithoutThrowing()
    {
        var dataDirectory = Path.Combine(
            Path.GetTempPath(),
            $"SystemCompassTests-{Guid.NewGuid():N}");
        var services = SystemServices.Create(dataDirectory);

        var entries = services.Audit.EnumerateAll();

        Assert.IsTrue(entries.Count > 0, "The audit returned no entries on a Windows machine.");
        Assert.IsTrue(
            entries.Any(entry => entry.Mechanism == StartupMechanism.Service),
            "No automatic service was found.");
        Assert.IsTrue(
            entries.All(entry => !string.IsNullOrWhiteSpace(entry.Id)),
            "Every entry needs a stable id.");
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void ProtectedRulesDoNotMatchArbitraryEntries()
    {
        var classifier = Classifier.FromEmbeddedRules();
        var assessment = classifier.Assess(TestData.Entry("Completely Unknown Tool 9271"));

        Assert.AreEqual(EntryCategory.Unclear, assessment.Category);
        Assert.IsFalse(assessment.Protected);
    }
}
