namespace SystemCompass.Core.Tweaks;

/// <summary>
/// Settings that weaken Windows security are refused here and never reach the
/// catalog. This is a product invariant, not a user preference.
/// </summary>
public static class SecurityDenyList
{
    private static readonly string[] DeniedKeyFragments =
    [
        "\\Policies\\Microsoft\\Windows Defender",
        "\\Policies\\Microsoft\\Windows\\DeviceGuard",
        "\\Policies\\Microsoft\\Windows\\WindowsUpdate",
        "\\Control\\DeviceGuard",
        "\\Control\\Lsa",
        "\\Services\\WinDefend",
        "\\Services\\WdNisSvc",
        "\\Services\\Sense",
    ];

    private static readonly string[] DeniedValueNames =
    [
        "DisableAntiSpyware",
        "DisableRealtimeMonitoring",
        "DisableBehaviorMonitoring",
        "DisableIOAVProtection",
        "DisableScriptScanning",
        "SpyNetReporting",
        "SubmitSamplesConsent",
        "DontReportInfectionInformation",
        "FeatureSettings",
        "FeatureSettingsOverride",
        "FeatureSettingsOverrideMask",
        "MitigationOptions",
        "MitigationAuditOptions",
        "EnableLUA",
        "ConsentPromptBehaviorAdmin",
        "FilterAdministratorToken",
        "LocalAccountTokenFilterPolicy",
        "EnableSmartScreen",
        "SmartScreenEnabled",
        "EnableWebContentEvaluation",
        "EnabledV9",
        "DisableRawSecurity",
        "SynAttackProtect",
        "EnableConnectionRateLimiting",
        "EncryptPagingFile",
    ];

    private static readonly string[] DeniedServices =
    [
        "WinDefend",
        "WdNisSvc",
        "Sense",
        "SecurityHealthService",
        "wuauserv",
        "BITS",
        "UsoSvc",
        "WaaSMedicSvc",
        "TrustedInstaller",
        "MpsSvc",
        "AppIDSvc",
        "SgrmBroker",
    ];

    private static readonly string[] DeniedTaskFragments =
    [
        "\\WindowsUpdate\\",
        "\\UpdateOrchestrator\\",
        "\\Windows Defender\\",
        "\\SmartScreenSpecific",
        "\\FamilySafety",
        "\\Defrag\\",
        "\\SystemRestore\\",
        "\\Backup\\",
        "\\Subscription\\",
        "\\License Manager\\",
        "\\PushToInstall\\",
        "\\Autochk\\",
        "\\WindowsStore\\",
    ];

    private static readonly string[] AllowedTcpGlobals =
    [
        "autotuninglevel",
        "rss",
        "rsc",
        "ecncapability",
        "timestamps",
        "nonsackrttresiliency",
        "congestionprovider",
    ];

    public static bool IsDenied(TweakOperation operation)
    {
        return operation switch
        {
            RegistrySet set => IsDeniedTarget($"{set.Hive}\\{set.KeyPath}", set.ValueName),
            RegistryDelete delete => IsDeniedTarget($"{delete.Hive}\\{delete.KeyPath}", delete.ValueName),
            ServiceStartType service => DeniedServices.Contains(service.ServiceName, StringComparer.OrdinalIgnoreCase),
            TcpGlobal tcp => !AllowedTcpGlobals.Contains(tcp.Name, StringComparer.OrdinalIgnoreCase),
            ScheduledTaskState task => DeniedTaskFragments.Any(fragment =>
                task.TaskPath.Contains(fragment, StringComparison.OrdinalIgnoreCase)),
            _ => false,
        };
    }

    private static bool IsDeniedTarget(string keyPath, string valueName)
    {
        return DeniedKeyFragments.Any(fragment =>
                   keyPath.Contains(fragment, StringComparison.OrdinalIgnoreCase))
               || DeniedValueNames.Contains(valueName, StringComparer.OrdinalIgnoreCase);
    }
}
