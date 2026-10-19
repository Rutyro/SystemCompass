using System.Text.Json.Serialization;

namespace SystemCompass.Core.Tweaks;

public enum TweakEvidence
{
    MicrosoftDocumented,
    Community,
    Unverified,
}

public enum TweakRisk
{
    Low,
    Medium,
    High,
}

public enum RegistryValueKindKind
{
    String,
    ExpandString,
    DWord,
    QWord,
    Binary,
}

public enum ServiceStartModeKind
{
    Boot,
    System,
    Automatic,
    Manual,
    Disabled,
}

public sealed record RegistryTarget(string Hive, string KeyPath, string ValueName);

public sealed record TweakValue(RegistryValueKindKind Kind, string Data);

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(RegistrySet), "registrySet")]
[JsonDerivedType(typeof(RegistryDelete), "registryDelete")]
[JsonDerivedType(typeof(ServiceStartType), "serviceStartType")]
[JsonDerivedType(typeof(PowerSetting), "powerSetting")]
public abstract record TweakOperation
{
    public abstract string Describe();
}

public sealed record RegistrySet(
    string Hive,
    string KeyPath,
    string ValueName,
    RegistryValueKindKind Kind,
    string Data) : TweakOperation
{
    public override string Describe() => $"Set {Hive}\\{KeyPath}\\{ValueName} = {Data} ({Kind})";
}

public sealed record RegistryDelete(
    string Hive,
    string KeyPath,
    string ValueName) : TweakOperation
{
    public override string Describe() => $"Delete {Hive}\\{KeyPath}\\{ValueName}";
}

public sealed record ServiceStartType(
    string ServiceName,
    ServiceStartModeKind Mode) : TweakOperation
{
    public override string Describe() => $"Service {ServiceName} start type = {Mode}";
}

public sealed record PowerSetting(
    Guid SubGroup,
    Guid Setting,
    uint AcValue,
    uint? DcValue) : TweakOperation
{
    public override string Describe() => DcValue is { } dc
        ? $"Power setting {Setting} AC={AcValue} DC={dc}"
        : $"Power setting {Setting} AC={AcValue}";
}

public sealed record TweakDefinition(
    string Id,
    string Title,
    string Description,
    string Category,
    TweakEvidence Evidence,
    IReadOnlyList<string> Links,
    TweakRisk Risk,
    int MinBuild,
    int MaxBuild,
    bool RequiresElevation,
    bool RequiresReboot,
    IReadOnlyList<TweakOperation> Operations)
{
    public bool IsApplicable(int osBuild) => osBuild >= MinBuild && osBuild <= MaxBuild;
}

public sealed record TweakOpState(
    bool Exists,
    TweakValue? Value,
    ServiceStartModeKind? ServiceMode,
    uint? PowerAc,
    uint? PowerDc);

public enum TweakStatus
{
    Applied,
    NotApplied,
    Partial,
    NotApplicable,
}

public sealed record TweakResult(
    bool Success,
    TweakStatus Status,
    IReadOnlyList<string> Steps,
    string? Error);

public interface ApplyTweaks
{
    TweakValue? ReadRegistry(RegistryTarget target);

    void WriteRegistry(RegistryTarget target, TweakValue value);

    void DeleteRegistry(RegistryTarget target);

    ServiceStartModeKind? ReadServiceStart(string serviceName);

    void SetServiceStart(string serviceName, ServiceStartModeKind mode);

    uint? ReadPowerSetting(Guid subGroup, Guid setting, bool ac);

    void WritePowerSetting(Guid subGroup, Guid setting, bool ac, uint value);
}
