namespace SystemCompass.Core.Startup;

public sealed class StartupAuditService
{
    private readonly IReadOnlyList<ManageStartup> _providers;

    public StartupAuditService(IEnumerable<ManageStartup> providers)
    {
        _providers = providers.ToArray();
    }

    public IReadOnlyList<StartupEntry> EnumerateAll()
    {
        return _providers
            .SelectMany(provider => provider.Enumerate())
            .OrderBy(entry => entry.Assessment.Category)
            .ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public StartupEntry? Find(string entryId)
    {
        return _providers
            .SelectMany(provider => provider.Enumerate())
            .FirstOrDefault(entry => entry.Id == entryId);
    }

    public bool CanChange(StartupEntry entry)
    {
        var provider = _providers.FirstOrDefault(candidate => candidate.Mechanism == entry.Mechanism);
        return provider is { CanSetState: true } && !entry.IsProtected;
    }
}
