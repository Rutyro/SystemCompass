using Microsoft.VisualStudio.TestTools.UnitTesting;
using SystemCompass.Core.Changes;
using SystemCompass.Core.Demo;
using SystemCompass.Core.Redaction;
using SystemCompass.Core.Reporting;
using SystemCompass.Core.Startup;

namespace SystemCompass.Tests;

[TestClass]
public sealed class RedactionAndReportTests
{
    [TestMethod]
    public void PersonalIdentifiersAreRedacted()
    {
        var redactor = new Redactor(new RedactionOptions
        {
            UserName = "alice",
            UserProfilePath = @"C:\Users\alice",
            MachineName = "ALICE-PC",
            ExtraPrefixes = [@"H:\Company"],
        });

        var redacted = redactor.Redact(
            @"ALICE-PC S-1-5-21-1-2-3 C:\Users\alice\AppData\Local\App\app.exe H:\Company\secret\data.txt J:\Other\file.txt");

        StringAssert.DoesNotMatch(redacted, new System.Text.RegularExpressions.Regex("alice"));
        StringAssert.DoesNotMatch(redacted, new System.Text.RegularExpressions.Regex("Company|Other"));
        StringAssert.Contains(redacted, "<USERPROFILE>");
        StringAssert.Contains(redacted, "<PATH>");
        StringAssert.Contains(redacted, "<DRIVE>");
        StringAssert.Contains(redacted, "<SID>");
        StringAssert.Contains(redacted, "<MACHINE>");
    }

    [TestMethod]
    public void ExtraLiteralCanBeAddedAtRuntime()
    {
        var redactor = new Redactor(new RedactionOptions());
        redactor.AddLiteral("Contoso");

        var redacted = redactor.Redact("Contoso project path");

        Assert.AreEqual("<REDACTED> project path", redacted);
    }

    [TestMethod]
    public void FreeTextReportFieldsAreRedacted()
    {
        var now = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var redactor = new Redactor(new RedactionOptions
        {
            UserName = "alice",
            UserProfilePath = @"C:\Users\alice",
            MachineName = "ALICE-PC",
        });
        var entry = TestData.Entry(
            @"C:\Users\alice\evil.lnk",
            signature: new SignatureInfo(SignatureStatus.Valid, "ALICE-PC signer"));
        var journal = new JournalRecord(
            "change1",
            now,
            entry.Id,
            @"C:\Users\alice\entry",
            "RunKey",
            "Disable",
            "Enabled",
            "Disabled",
            ChangeStatus.Failed,
            @"ALICE-PC could not open C:\Users\alice\error");
        var report = new DiagnosticsReport(
            now,
            "0.1.0",
            "Windows ALICE-PC",
            [new DiagnosticsSection("Section", [new DiagnosticsItem(@"C:\Users\alice\name", @"C:\Users\alice\value")])],
            [entry],
            [journal],
            new BootMeasurement(false, null, null, "test"));
        var builder = new ReportBuilder(redactor);

        var markdown = builder.BuildMarkdown(report);
        var json = builder.BuildJson(report);
        foreach (var text in new[] { markdown, json })
        {
            StringAssert.DoesNotMatch(text, new System.Text.RegularExpressions.Regex("alice"));
            StringAssert.DoesNotMatch(text, new System.Text.RegularExpressions.Regex("ALICE-PC"));
            StringAssert.Contains(text, "USERPROFILE");
            StringAssert.Contains(text, "MACHINE");
        }

        StringAssert.Contains(markdown, "<USERPROFILE>");
        StringAssert.Contains(markdown, "<MACHINE>");
    }

    [TestMethod]
    public void DemoReportContainsNoSeedIdentifiers()
    {
        var now = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var report = DemoDataSet.CreateReport(now, "0.1.0");
        var builder = new ReportBuilder(DemoDataSet.CreateRedactor());

        var markdown = builder.BuildMarkdown(report);
        var json = builder.BuildJson(report);

        foreach (var text in new[] { markdown, json })
        {
            StringAssert.DoesNotMatch(text, new System.Text.RegularExpressions.Regex("demo-user"));
            StringAssert.DoesNotMatch(text, new System.Text.RegularExpressions.Regex("COMPASS-DEMO"));
            StringAssert.DoesNotMatch(text, new System.Text.RegularExpressions.Regex("H:\\\\Company"));
        }

        StringAssert.Contains(markdown, "SystemCompass startup audit report");
        StringAssert.Contains(json, "\"startup\"");
    }
}
