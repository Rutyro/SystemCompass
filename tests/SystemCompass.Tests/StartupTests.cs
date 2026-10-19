using Microsoft.VisualStudio.TestTools.UnitTesting;
using SystemCompass.Core.Startup;

namespace SystemCompass.Tests;

[TestClass]
public sealed class StartupTests
{
    [TestMethod]
    public void MissingApprovalValueMeansEnabled()
    {
        Assert.AreEqual(EntryState.Enabled, StartupApprovedValue.Parse(ReadOnlySpan<byte>.Empty));
    }

    [TestMethod]
    [DataRow((byte)0x02, EntryState.Enabled)]
    [DataRow((byte)0x06, EntryState.Enabled)]
    [DataRow((byte)0x03, EntryState.Disabled)]
    [DataRow((byte)0x07, EntryState.Disabled)]
    [DataRow((byte)0x01, EntryState.Unknown)]
    public void ApprovalFlagIsInterpreted(byte flag, EntryState expected)
    {
        var value = new byte[12];
        value[0] = flag;
        Assert.AreEqual(expected, StartupApprovedValue.Parse(value));
    }

    [TestMethod]
    public void CreatedApprovalValueRoundTrips()
    {
        var enabled = StartupApprovedValue.Create(true, DateTimeOffset.UnixEpoch);
        var disabled = StartupApprovedValue.Create(false, DateTimeOffset.UnixEpoch);

        Assert.AreEqual(EntryState.Enabled, StartupApprovedValue.Parse(enabled));
        Assert.AreEqual(EntryState.Disabled, StartupApprovedValue.Parse(disabled));
        Assert.AreEqual(12, enabled.Length);
    }

    [TestMethod]
    [DataRow("\"C:\\Program Files\\App\\app.exe\" --minimized", "C:\\Program Files\\App\\app.exe")]
    [DataRow("C:\\Windows\\System32\\ctfmon.exe", "C:\\Windows\\System32\\ctfmon.exe")]
    [DataRow("C:\\Windows\\System32\\rundll32.exe shell32.dll,Control_RunDLL", "C:\\Windows\\System32\\rundll32.exe")]
    public void ExecutableIsExtracted(string commandLine, string expected)
    {
        Assert.AreEqual(expected, CommandLineParser.TryGetExecutable(commandLine));
    }

    [TestMethod]
    public void EnvironmentVariablesAreExpanded()
    {
        var parsed = CommandLineParser.TryGetExecutable("%SystemRoot%\\System32\\notepad.exe");
        Assert.IsNotNull(parsed);
        Assert.IsTrue(parsed.EndsWith("notepad.exe", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void EmptyCommandLineHasNoExecutable()
    {
        Assert.IsNull(CommandLineParser.TryGetExecutable("   "));
        Assert.IsNull(CommandLineParser.TryGetExecutable(null));
    }

    [TestMethod]
    public void EmbeddedRulesClassifyTeamViewerAsProtected()
    {
        var classifier = Classifier.FromEmbeddedRules();
        var entry = TestData.Entry(
            "TeamViewer",
            mechanism: StartupMechanism.Service,
            signature: new SignatureInfo(SignatureStatus.Valid, "CN=TeamViewer Germany GmbH"));

        var assessment = classifier.Assess(entry);

        Assert.AreEqual(EntryCategory.SecurityRelevant, assessment.Category);
        Assert.IsTrue(assessment.Protected);
    }

    [TestMethod]
    public void UnknownEntryIsUnclearAndNotProtected()
    {
        var classifier = Classifier.FromEmbeddedRules();
        var assessment = classifier.Assess(TestData.Entry("Some Unknown Tool 42"));

        Assert.AreEqual(EntryCategory.Unclear, assessment.Category);
        Assert.IsFalse(assessment.Protected);
    }

    [TestMethod]
    public void EntryIdIsStable()
    {
        var first = StartupEntryId.Create(StartupMechanism.RunKey, StartupScope.CurrentUser, "a", "b");
        var second = StartupEntryId.Create(StartupMechanism.RunKey, StartupScope.CurrentUser, "a", "b");
        var other = StartupEntryId.Create(StartupMechanism.Service, StartupScope.CurrentUser, "a", "b");

        Assert.AreEqual(first, second);
        Assert.AreNotEqual(first, other);
    }

    [TestMethod]
    public void NetworkPathsAreNotTreatedAsLocalFiles()
    {
        Assert.IsTrue(LocalPath.IsLocalFile(@"C:\Windows\notepad.exe"));
        Assert.IsTrue(LocalPath.IsLocalFile(@"\\?\C:\Windows\notepad.exe"));
        Assert.IsFalse(LocalPath.IsLocalFile(@"\\server\share\evil.exe"));
        Assert.IsFalse(LocalPath.IsLocalFile(@"\\?\UNC\server\share\evil.exe"));
        Assert.IsFalse(LocalPath.IsLocalFile(@"\\.\pipe\evil"));
        Assert.IsFalse(LocalPath.IsLocalFile(@"relative\evil.exe"));
    }
}
