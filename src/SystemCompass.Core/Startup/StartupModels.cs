using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace SystemCompass.Core.Startup;

public enum StartupMechanism
{
    RunKey,
    RunOnceKey,
    StartupFolder,
    ScheduledTask,
    Service,
    Winlogon,
}

public enum StartupScope
{
    CurrentUser,
    AllUsers,
    System,
}

public enum EntryState
{
    Enabled,
    Disabled,
    Unknown,
}

public enum EntryCategory
{
    Keep,
    Optional,
    DisableRecommended,
    Unclear,
    SystemCritical,
    SecurityRelevant,
}

public enum RiskLevel
{
    Low,
    Medium,
    High,
    Critical,
}

public enum SignatureStatus
{
    Valid,
    ValidCatalog,
    Unsigned,
    Invalid,
    Unknown,
}

public sealed record SignatureInfo(SignatureStatus Status, string? Signer = null, string? Thumbprint = null)
{
    public static SignatureInfo Unknown { get; } = new(SignatureStatus.Unknown);
}

public sealed record Assessment(
    EntryCategory Category,
    RiskLevel Risk,
    string Reason,
    string Recovery,
    bool Protected = false);

public sealed record StartupEntry(
    string Id,
    string Name,
    string Location,
    StartupMechanism Mechanism,
    StartupScope Scope,
    EntryState State,
    string? CommandLine,
    string? ResolvedPath,
    SignatureInfo Signature,
    Assessment Assessment,
    bool RequiresElevation)
{
    public bool IsProtected => Assessment.Protected
        || Assessment.Category is EntryCategory.SystemCritical or EntryCategory.SecurityRelevant;
}

public static class StartupEntryId
{
    public static string Create(StartupMechanism mechanism, StartupScope scope, string location, string name)
    {
        var key = string.Create(
            CultureInfo.InvariantCulture,
            $"{mechanism}|{scope}|{location}|{name}");
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        return Convert.ToHexStringLower(hash)[..16];
    }
}
