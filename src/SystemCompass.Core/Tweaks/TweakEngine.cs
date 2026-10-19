using System.ComponentModel;
using System.Text.Json;
using SystemCompass.Core.Changes;

namespace SystemCompass.Core.Tweaks;

/// <summary>
/// Applies catalog tweaks with a captured previous state, verifies every
/// operation by reading the system back, and records everything in the journal.
/// A failed apply reverts the operations that already succeeded.
/// </summary>
public sealed class TweakEngine
{
    private static readonly JsonSerializerOptions StateJson = new();

    private readonly ApplyTweaks _backend;
    private readonly RecordChanges _journal;
    private readonly TimeProvider _clock;

    public TweakEngine(ApplyTweaks backend, RecordChanges journal, TimeProvider clock)
    {
        _backend = backend;
        _journal = journal;
        _clock = clock;
    }

    public TweakStatus GetStatus(TweakDefinition tweak, int osBuild)
    {
        if (!tweak.IsApplicable(osBuild))
        {
            return TweakStatus.NotApplicable;
        }

        var applied = tweak.Operations.Count(IsApplied);
        return applied switch
        {
            0 => TweakStatus.NotApplied,
            _ when applied == tweak.Operations.Count => TweakStatus.Applied,
            _ => TweakStatus.Partial,
        };
    }

    public TweakResult Apply(TweakDefinition tweak, int osBuild)
    {
        if (!tweak.IsApplicable(osBuild))
        {
            return new TweakResult(false, TweakStatus.NotApplicable, [],
                $"This tweak applies to Windows builds {tweak.MinBuild}-{tweak.MaxBuild}.");
        }

        var denied = tweak.Operations.FirstOrDefault(SecurityDenyList.IsDenied);
        if (denied is not null)
        {
            return new TweakResult(false, TweakStatus.NotApplicable, [],
                $"Refused: {denied.Describe()} targets a security-protected setting.");
        }

        var states = tweak.Operations.Select(Capture).ToArray();
        var changeId = Guid.NewGuid().ToString("N")[..12];
        var steps = new List<string>();
        var applied = 0;

        try
        {
            for (var index = 0; index < tweak.Operations.Count; index++)
            {
                var operation = tweak.Operations[index];
                Log(tweak, index, changeId, "Apply", states[index], ChangeStatus.Pending, null);
                Execute(operation);

                if (!IsApplied(operation))
                {
                    Log(tweak, index, changeId, "Apply", states[index], ChangeStatus.Failed,
                        "Verification failed.");
                    throw new InvalidOperationException(
                        $"Verification failed for operation {index + 1}: {operation.Describe()}");
                }

                Log(tweak, index, changeId, "Apply", states[index], ChangeStatus.Verified, null);
                applied++;
                steps.Add(operation.Describe());
            }
        }
        catch (Exception ex) when (ex is IOException
            or UnauthorizedAccessException
            or InvalidOperationException
            or Win32Exception)
        {
            var restoreErrors = new List<string>();
            for (var index = applied - 1; index >= 0; index--)
            {
                try
                {
                    Restore(tweak.Operations[index], states[index]);
                }
                catch (Exception restoreException) when (restoreException is IOException
                    or UnauthorizedAccessException
                    or InvalidOperationException
                    or Win32Exception)
                {
                    restoreErrors.Add($"Operation {index + 1}: {restoreException.Message}");
                }
            }

            var message = restoreErrors.Count == 0
                ? ex.Message
                : $"{ex.Message} Automatic revert incomplete: {string.Join("; ", restoreErrors)}";
            return new TweakResult(false, TweakStatus.Partial, steps, message);
        }

        return new TweakResult(true, TweakStatus.Applied, steps, null);
    }

    public TweakResult Revert(TweakDefinition tweak)
    {
        var records = _journal.ReadAll();
        var changeId = Guid.NewGuid().ToString("N")[..12];
        var steps = new List<string>();

        try
        {
            for (var index = tweak.Operations.Count - 1; index >= 0; index--)
            {
                var targetId = $"{tweak.Id}#{index}";
                var record = records.LastOrDefault(candidate =>
                    candidate.EntryId == targetId
                    && candidate.Action == "Apply"
                    && candidate.Status == ChangeStatus.Verified);
                if (record?.Before is null)
                {
                    steps.Add($"Operation {index + 1}: no recorded previous state, left unchanged.");
                    continue;
                }

                var state = JsonSerializer.Deserialize<TweakOpState>(record.Before, StateJson);
                if (state is null)
                {
                    steps.Add($"Operation {index + 1}: recorded state could not be read, left unchanged.");
                    continue;
                }

                Restore(tweak.Operations[index], state);
                Log(tweak, index, changeId, "Revert", state, ChangeStatus.Verified, null);
                steps.Add($"Operation {index + 1}: restored.");
            }
        }
        catch (Exception ex) when (ex is IOException
            or UnauthorizedAccessException
            or InvalidOperationException
            or Win32Exception)
        {
            return new TweakResult(false, TweakStatus.Partial, steps, ex.Message);
        }

        return new TweakResult(true, TweakStatus.NotApplied, steps, null);
    }

    private static RegistryTarget Target(RegistrySet set) => new(set.Hive, set.KeyPath, set.ValueName);

    private static RegistryTarget Target(RegistryDelete delete) => new(delete.Hive, delete.KeyPath, delete.ValueName);

    private TweakOpState Capture(TweakOperation operation)
    {
        return operation switch
        {
            RegistrySet set => ReadRegistryState(Target(set)),
            RegistryDelete delete => ReadRegistryState(Target(delete)),
            ServiceStartType service => new TweakOpState(
                true, null, _backend.ReadServiceStart(service.ServiceName), null, null),
            PowerSetting power => new TweakOpState(
                true,
                null,
                null,
                _backend.ReadPowerSetting(power.SubGroup, power.Setting, true),
                _backend.ReadPowerSetting(power.SubGroup, power.Setting, false)),
            _ => throw new NotSupportedException(operation.GetType().Name),
        };
    }

    private TweakOpState ReadRegistryState(RegistryTarget target)
    {
        var value = _backend.ReadRegistry(target);
        return new TweakOpState(value is not null, value, null, null, null);
    }

    private void Execute(TweakOperation operation)
    {
        switch (operation)
        {
            case RegistrySet set:
                _backend.WriteRegistry(Target(set), new TweakValue(set.Kind, set.Data));
                break;
            case RegistryDelete delete:
                _backend.DeleteRegistry(Target(delete));
                break;
            case ServiceStartType service:
                _backend.SetServiceStart(service.ServiceName, service.Mode);
                break;
            case PowerSetting power:
                _backend.WritePowerSetting(power.SubGroup, power.Setting, true, power.AcValue);
                if (power.DcValue is { } dc)
                {
                    _backend.WritePowerSetting(power.SubGroup, power.Setting, false, dc);
                }

                break;
            default:
                throw new NotSupportedException(operation.GetType().Name);
        }
    }

    private void Restore(TweakOperation operation, TweakOpState state)
    {
        switch (operation)
        {
            case RegistrySet set:
                RestoreRegistry(Target(set), state);
                break;
            case RegistryDelete delete:
                RestoreRegistry(Target(delete), state);
                break;
            case ServiceStartType service when state.ServiceMode is { } mode:
                _backend.SetServiceStart(service.ServiceName, mode);
                break;
            case PowerSetting power:
                if (state.PowerAc is { } ac)
                {
                    _backend.WritePowerSetting(power.SubGroup, power.Setting, true, ac);
                }

                if (state.PowerDc is { } dc)
                {
                    _backend.WritePowerSetting(power.SubGroup, power.Setting, false, dc);
                }

                break;
            default:
                break;
        }
    }

    private void RestoreRegistry(RegistryTarget target, TweakOpState state)
    {
        if (state.Value is null)
        {
            _backend.DeleteRegistry(target);
        }
        else
        {
            _backend.WriteRegistry(target, state.Value);
        }
    }

    private bool IsApplied(TweakOperation operation)
    {
        return operation switch
        {
            RegistrySet set => _backend.ReadRegistry(Target(set)) is { } actual
                && actual.Kind == set.Kind
                && string.Equals(actual.Data, set.Data, StringComparison.OrdinalIgnoreCase),
            RegistryDelete delete => _backend.ReadRegistry(Target(delete)) is null,
            ServiceStartType service => _backend.ReadServiceStart(service.ServiceName) == service.Mode,
            PowerSetting power => _backend.ReadPowerSetting(power.SubGroup, power.Setting, true) == power.AcValue
                && (power.DcValue is null
                    || _backend.ReadPowerSetting(power.SubGroup, power.Setting, false) == power.DcValue),
            _ => false,
        };
    }

    private void Log(
        TweakDefinition tweak,
        int operationIndex,
        string changeId,
        string action,
        TweakOpState state,
        ChangeStatus status,
        string? error)
    {
        _journal.Append(new JournalRecord(
            changeId,
            _clock.GetUtcNow(),
            $"{tweak.Id}#{operationIndex}",
            tweak.Title,
            "Tweak",
            action,
            JsonSerializer.Serialize(state, StateJson),
            null,
            status,
            error));
    }
}
