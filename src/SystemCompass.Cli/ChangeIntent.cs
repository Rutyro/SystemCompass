namespace SystemCompass.Cli;

internal sealed record ChangeIntent(IReadOnlyList<IntentEntry> Entries);

internal sealed record IntentEntry(string EntryId, bool Enabled);

internal sealed record IntentResult(string EntryId, bool Success, string Message);

internal sealed record IntentResultFile(IReadOnlyList<IntentResult> Results);
