using Microsoft.VisualStudio.TestTools.UnitTesting;
using SystemCompass.Core.Demo;
using SystemCompass.Core.Redaction;
using SystemCompass.Core.Reporting;

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
