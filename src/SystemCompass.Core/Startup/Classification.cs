using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace SystemCompass.Core.Startup;

public sealed record ClassificationRule
{
    public required string Id { get; init; }

    public StartupMechanism? Mechanism { get; init; }

    public StartupScope? Scope { get; init; }

    public string? NamePattern { get; init; }

    public string? PathPrefix { get; init; }

    public string? LocationPrefix { get; init; }

    public string? SignerContains { get; init; }

    public EntryCategory Category { get; init; } = EntryCategory.Unclear;

    public RiskLevel Risk { get; init; } = RiskLevel.Medium;

    public bool Protected { get; init; }

    public string Reason { get; init; } = string.Empty;

    public string Recovery { get; init; } = string.Empty;
}

public sealed class Classifier
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly IReadOnlyList<CompiledRule> _rules;

    public Classifier(IEnumerable<ClassificationRule> rules)
    {
        _rules = rules
            .Select(rule => new CompiledRule(
                rule,
                string.IsNullOrEmpty(rule.NamePattern) ? null : Wildcard.Create(rule.NamePattern)))
            .ToArray();
    }

    public static Classifier FromEmbeddedRules()
    {
        using var stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("SystemCompass.Core.Rules.rules.json")
            ?? throw new InvalidOperationException("Embedded startup rules are missing.");
        var file = JsonSerializer.Deserialize<RuleFile>(stream, JsonOptions)
            ?? throw new InvalidOperationException("Embedded startup rules are empty.");
        return new Classifier(file.Rules);
    }

    public Assessment Assess(StartupEntry entry)
    {
        foreach (var compiled in _rules)
        {
            if (Matches(compiled, entry))
            {
                var rule = compiled.Rule;
                return new Assessment(rule.Category, rule.Risk, rule.Reason, rule.Recovery, rule.Protected);
            }
        }

        return new Assessment(
            EntryCategory.Unclear,
            RiskLevel.Medium,
            "No classification rule matched. Review this entry before changing it.",
            "Re-enable through Task Manager or the original application.");
    }

    private static bool Matches(CompiledRule compiled, StartupEntry entry)
    {
        var rule = compiled.Rule;
        if (rule.Mechanism is { } mechanism && mechanism != entry.Mechanism)
        {
            return false;
        }

        if (rule.Scope is { } scope && scope != entry.Scope)
        {
            return false;
        }

        if (compiled.NameRegex is not null && !compiled.NameRegex.IsMatch(entry.Name))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(rule.PathPrefix)
            && (entry.ResolvedPath is null
                || !entry.ResolvedPath.StartsWith(rule.PathPrefix, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(rule.LocationPrefix)
            && !entry.Location.StartsWith(rule.LocationPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(rule.SignerContains)
            && (entry.Signature.Signer is null
                || !entry.Signature.Signer.Contains(rule.SignerContains, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        return true;
    }

    private sealed record RuleFile(IReadOnlyList<ClassificationRule> Rules);

    private sealed record CompiledRule(ClassificationRule Rule, Regex? NameRegex);
}

internal static class Wildcard
{
    public static Regex Create(string pattern)
    {
        var expression = "^" + Regex.Escape(pattern).Replace("\\*", ".*", StringComparison.Ordinal) + "$";
        return new Regex(
            expression,
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled,
            TimeSpan.FromSeconds(1));
    }
}
