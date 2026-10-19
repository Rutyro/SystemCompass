using System.Text.Json;
using System.Text.Json.Serialization;

namespace SystemCompass.Core.Changes;

public enum ChangeStatus
{
    Pending,
    Verified,
    Failed,
    RolledBack,
}

public sealed record JournalRecord(
    string ChangeId,
    DateTimeOffset Timestamp,
    string EntryId,
    string EntryName,
    string Mechanism,
    string Action,
    string? Before,
    string? After,
    ChangeStatus Status,
    string? Error = null);

public interface RecordChanges
{
    void Append(JournalRecord record);

    IReadOnlyList<JournalRecord> ReadAll();
}

public sealed class JsonLinesJournal : RecordChanges
{
    private static readonly JsonSerializerOptions Options = new()
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly Lock _gate = new();

    public JsonLinesJournal(string filePath)
    {
        FilePath = filePath;
    }

    public string FilePath { get; }

    public void Append(JournalRecord record)
    {
        var directory = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var line = JsonSerializer.Serialize(record, Options);
        lock (_gate)
        {
            File.AppendAllText(FilePath, line + Environment.NewLine);
        }
    }

    public IReadOnlyList<JournalRecord> ReadAll()
    {
        if (!File.Exists(FilePath))
        {
            return Array.Empty<JournalRecord>();
        }

        var records = new List<JournalRecord>();
        lock (_gate)
        {
            foreach (var line in File.ReadLines(FilePath))
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                try
                {
                    var record = JsonSerializer.Deserialize<JournalRecord>(line, Options);
                    if (record is not null)
                    {
                        records.Add(record);
                    }
                }
                catch (JsonException)
                {
                    // A torn final line from a crash must not make the journal unreadable.
                }
            }
        }

        return records;
    }
}

public sealed class InMemoryJournal : RecordChanges
{
    private readonly List<JournalRecord> _records = [];

    public void Append(JournalRecord record) => _records.Add(record);

    public IReadOnlyList<JournalRecord> ReadAll() => _records.ToArray();
}

public static class JournalView
{
    /// <summary>
    /// Append-only records are written as Pending and then updated by appending
    /// a final record with the same change id. The last one wins.
    /// </summary>
    public static IReadOnlyList<JournalRecord> LatestPerChange(IEnumerable<JournalRecord> records)
    {
        return records
            .GroupBy(record => record.ChangeId)
            .Select(group => group.Last())
            .OrderByDescending(record => record.Timestamp)
            .ToArray();
    }
}
