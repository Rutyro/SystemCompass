using SystemCompass.Core.Startup;

namespace SystemCompass.Tests;

internal static class TestData
{
    public static StartupEntry Entry(
        string name,
        EntryState state = EntryState.Enabled,
        StartupMechanism mechanism = StartupMechanism.RunKey,
        SignatureInfo? signature = null,
        string? path = null,
        bool isProtected = false)
    {
        return new StartupEntry(
            StartupEntryId.Create(mechanism, StartupScope.CurrentUser, "test", name),
            name,
            "test",
            mechanism,
            StartupScope.CurrentUser,
            state,
            path,
            path,
            signature ?? SignatureInfo.Unknown,
            new Assessment(EntryCategory.Optional, RiskLevel.Low, "test", "test", isProtected),
            false);
    }
}
