using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace SystemCompass.Core.Tweaks;

public sealed class TweakCatalog
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private TweakCatalog(IReadOnlyList<TweakDefinition> tweaks)
    {
        Tweaks = tweaks;
    }

    public IReadOnlyList<TweakDefinition> Tweaks { get; }

    public static TweakCatalog Empty { get; } = new(Array.Empty<TweakDefinition>());

    public static TweakCatalog FromEmbedded()
    {
        using var stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("SystemCompass.Core.Tweaks.catalog.json")
            ?? throw new InvalidOperationException("Embedded tweak catalog is missing.");
        var file = JsonSerializer.Deserialize<CatalogFile>(stream, JsonOptions)
            ?? throw new InvalidOperationException("Embedded tweak catalog is empty.");
        return new TweakCatalog(file.Tweaks);
    }

    /// <summary>
    /// Returns human-readable problems. An empty list means the catalog may ship.
    /// Tests call this; the UI hides a catalog that fails validation.
    /// </summary>
    public IReadOnlyList<string> Validate()
    {
        var problems = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var tweak in Tweaks)
        {
            if (!seen.Add(tweak.Id))
            {
                problems.Add($"Duplicate tweak id '{tweak.Id}'.");
            }

            if (!Regex.IsMatch(tweak.Id, "^[a-z0-9]+(-[a-z0-9]+)*$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)))
            {
                problems.Add($"Tweak id '{tweak.Id}' is not lower-kebab-case.");
            }

            if (string.IsNullOrWhiteSpace(tweak.Title) || string.IsNullOrWhiteSpace(tweak.Description))
            {
                problems.Add($"Tweak '{tweak.Id}' needs a title and description.");
            }

            if (tweak.Operations.Count == 0)
            {
                problems.Add($"Tweak '{tweak.Id}' has no operations.");
            }

            if (tweak.MinBuild > tweak.MaxBuild)
            {
                problems.Add($"Tweak '{tweak.Id}' has an inverted OS range.");
            }

            if (tweak.Evidence == TweakEvidence.MicrosoftDocumented
                && !tweak.Links.Any(link => link.Contains("learn.microsoft.com", StringComparison.OrdinalIgnoreCase)))
            {
                problems.Add($"Tweak '{tweak.Id}' claims Microsoft documentation but has no learn.microsoft.com link.");
            }

            foreach (var operation in tweak.Operations)
            {
                if (SecurityDenyList.IsDenied(operation))
                {
                    problems.Add($"Tweak '{tweak.Id}' targets a security-protected setting: {operation.Describe()}");
                }
            }
        }

        return problems;
    }

    private sealed record CatalogFile(IReadOnlyList<TweakDefinition> Tweaks);
}
