namespace SystemCompass.Core.Startup;

public interface ManageStartup
{
    StartupMechanism Mechanism { get; }

    bool CanSetState { get; }

    IReadOnlyList<StartupEntry> Enumerate();

    void SetState(string entryId, bool enabled);

    /// <summary>
    /// Reads one entry's current state. Providers whose enumeration intentionally
    /// covers only enabled entries (services) override this.
    /// </summary>
    EntryState? GetState(string entryId)
        => Enumerate().FirstOrDefault(entry => entry.Id == entryId)?.State;
}
