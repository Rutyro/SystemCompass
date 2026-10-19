using System.Text.Json;
using SystemCompass.Core.Startup;

namespace SystemCompass.Core.Changes;

public sealed record SnapshotEntry(string Id, string Name, string Mechanism, string State);

public sealed record Snapshot(DateTimeOffset TakenAt, IReadOnlyList<SnapshotEntry> Entries)
{
    public static Snapshot From(IEnumerable<StartupEntry> entries, DateTimeOffset takenAt)
    {
        return new Snapshot(
            takenAt,
            entries.Select(entry => new SnapshotEntry(
                entry.Id,
                entry.Name,
                entry.Mechanism.ToString(),
                entry.State.ToString())).ToArray());
    }
}

public enum SnapshotChangeKind
{
    New,
    Removed,
    ReEnabled,
    Changed,
}

public sealed record SnapshotChange(SnapshotChangeKind Kind, SnapshotEntry Entry, string? PreviousState);

public static class SnapshotComparer
{
    public static IReadOnlyList<SnapshotChange> Compare(Snapshot previous, Snapshot current)
    {
        var before = previous.Entries.ToDictionary(entry => entry.Id);
        var after = current.Entries.ToDictionary(entry => entry.Id);
        var changes = new List<SnapshotChange>();

        foreach (var entry in after.Values)
        {
            if (!before.TryGetValue(entry.Id, out var old))
            {
                changes.Add(new SnapshotChange(SnapshotChangeKind.New, entry, null));
                continue;
            }

            if (old.State == entry.State)
            {
                continue;
            }

            var kind = old.State == nameof(EntryState.Disabled)
                && entry.State == nameof(EntryState.Enabled)
                    ? SnapshotChangeKind.ReEnabled
                    : SnapshotChangeKind.Changed;
            changes.Add(new SnapshotChange(kind, entry, old.State));
        }

        foreach (var entry in before.Values.Where(entry => !after.ContainsKey(entry.Id)))
        {
            changes.Add(new SnapshotChange(SnapshotChangeKind.Removed, entry, entry.State));
        }

        return changes;
    }
}

public sealed class SnapshotStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    private readonly string _directory;

    public SnapshotStore(string directory)
    {
        _directory = directory;
    }

    public void Save(Snapshot snapshot)
    {
        Directory.CreateDirectory(_directory);
        var name = $"snapshot-{snapshot.TakenAt:yyyyMMdd-HHmmss}.json";
        File.WriteAllText(Path.Combine(_directory, name), JsonSerializer.Serialize(snapshot, Options));
    }

    public Snapshot? LoadLatest()
    {
        if (!Directory.Exists(_directory))
        {
            return null;
        }

        var file = new DirectoryInfo(_directory)
            .GetFiles("snapshot-*.json")
            .OrderByDescending(candidate => candidate.Name, StringComparer.Ordinal)
            .FirstOrDefault();
        return file is null
            ? null
            : JsonSerializer.Deserialize<Snapshot>(File.ReadAllText(file.FullName), Options);
    }
}
