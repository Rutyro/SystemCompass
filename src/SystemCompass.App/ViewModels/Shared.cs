using System.Collections.ObjectModel;
using System.Windows.Media;

namespace SystemCompass.App.ViewModels;

public sealed record ChartSlice(
    string Label,
    int Value,
    double Fraction,
    string ValueLabel,
    string PercentLabel,
    Brush Brush);

public sealed record ChartBar(
    string Label,
    int Value,
    double Fraction,
    string ValueLabel,
    Brush Brush);

/// <summary>One node in the shell's explorer tree.</summary>
public sealed class ExplorerNode
{
    public ExplorerNode(string name, string tag, IEnumerable<ExplorerNode>? children = null, object? payload = null)
    {
        Name = name;
        Tag = tag;
        Children = new ObservableCollection<ExplorerNode>(children ?? []);
        Payload = payload;
    }

    public string Name { get; }

    public string Tag { get; }

    public object? Payload { get; }

    public ObservableCollection<ExplorerNode> Children { get; }
}
