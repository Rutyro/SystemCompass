using System.ComponentModel;
using SystemCompass.Core.Startup;

namespace SystemCompass.Core.Changes;

public sealed record ChangeResult(
    bool Success,
    string ChangeId,
    string Message,
    EntryState? ObservedState);

/// <summary>
/// Applies one startup-state change, verifies it by re-reading the system and
/// records both steps in the append-only journal. A GUI toggle is only shown as
/// changed after the observed state matches the requested one.
/// </summary>
public sealed class ChangeExecutor
{
    private readonly IReadOnlyList<ManageStartup> _providers;
    private readonly RecordChanges _journal;
    private readonly TimeProvider _clock;

    public ChangeExecutor(
        IEnumerable<ManageStartup> providers,
        RecordChanges journal,
        TimeProvider clock)
    {
        _providers = providers.ToArray();
        _journal = journal;
        _clock = clock;
    }

    public ChangeResult SetState(StartupEntry entry, bool enabled)
    {
        var expected = enabled ? EntryState.Enabled : EntryState.Disabled;
        var action = enabled ? "Enable" : "Disable";
        return Apply(entry, enabled, action, expected);
    }

    public ChangeResult Rollback(string changeId)
    {
        var latest = JournalView.LatestPerChange(_journal.ReadAll())
            .FirstOrDefault(record => record.ChangeId == changeId);
        if (latest is null)
        {
            return new ChangeResult(false, changeId, "Unknown change.", null);
        }

        if (latest.Status != ChangeStatus.Verified)
        {
            return new ChangeResult(false, changeId, "Only verified changes can be rolled back.", null);
        }

        if (!Enum.TryParse<EntryState>(latest.Before, out var previous) || previous == EntryState.Unknown)
        {
            return new ChangeResult(false, changeId, "The original state was not recorded; refusing to guess.", null);
        }

        var entry = _providers
            .SelectMany(provider => provider.Enumerate())
            .FirstOrDefault(candidate => candidate.Id == latest.EntryId);
        if (entry is null)
        {
            return new ChangeResult(false, changeId, "The entry no longer exists.", null);
        }

        var enabled = previous == EntryState.Enabled;
        return Apply(entry, enabled, $"Rollback of {changeId}", previous);
    }

    private ChangeResult Apply(StartupEntry entry, bool enabled, string action, EntryState expected)
    {
        var changeId = Guid.NewGuid().ToString("N")[..12];
        if (entry.IsProtected)
        {
            return new ChangeResult(
                false,
                changeId,
                "This entry is protected and cannot be changed.",
                entry.State);
        }

        var provider = _providers.FirstOrDefault(candidate => candidate.Mechanism == entry.Mechanism);
        if (provider is null)
        {
            return Fail(changeId, entry, action, "No provider handles this mechanism.");
        }

        if (!provider.CanSetState)
        {
            return Fail(changeId, entry, action, "This mechanism is read-only.");
        }

        var before = entry.State;
        Log(changeId, entry, action, before, null, ChangeStatus.Pending, null);

        try
        {
            provider.SetState(entry.Id, enabled);
            var observed = provider.GetState(entry.Id) ?? EntryState.Unknown;

            if (observed != expected)
            {
                var message = $"Verification failed: expected {expected}, observed {observed}.";
                Log(changeId, entry, action, before, observed, ChangeStatus.Failed, message);
                return new ChangeResult(false, changeId, message, observed);
            }

            Log(changeId, entry, action, before, observed, ChangeStatus.Verified, null);
            var verb = enabled ? "Enabled" : "Disabled";
            return new ChangeResult(true, changeId, $"{verb} {entry.Name}.", observed);
        }
        catch (Exception ex) when (ex is IOException
            or UnauthorizedAccessException
            or InvalidOperationException
            or Win32Exception)
        {
            Log(changeId, entry, action, before, null, ChangeStatus.Failed, ex.Message);
            return new ChangeResult(false, changeId, ex.Message, null);
        }
    }

    private ChangeResult Fail(string changeId, StartupEntry entry, string action, string message)
    {
        Log(changeId, entry, action, entry.State, null, ChangeStatus.Failed, message);
        return new ChangeResult(false, changeId, message, null);
    }

    private void Log(
        string changeId,
        StartupEntry entry,
        string action,
        EntryState? before,
        EntryState? after,
        ChangeStatus status,
        string? error)
    {
        _journal.Append(new JournalRecord(
            changeId,
            _clock.GetUtcNow(),
            entry.Id,
            entry.Name,
            entry.Mechanism.ToString(),
            action,
            before?.ToString(),
            after?.ToString(),
            status,
            error));
    }
}
