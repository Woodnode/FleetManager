using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AutoNexusMobile.Controls;

/// <summary>Option d'un groupe de puces (filtre de statut, type d'intervention, enseigne…).</summary>
public sealed partial class Choice : ObservableObject
{
    private readonly Action<Choice> _onSelected;

    public Choice(string label, object? value, Action<Choice> onSelected, bool isSelected = false)
    {
        Label = label;
        Value = value;
        _onSelected = onSelected;
        _isSelected = isSelected;
    }

    public string Label { get; }

    public object? Value { get; }

    [ObservableProperty]
    private bool _isSelected;

    [RelayCommand]
    private void Select() => _onSelected(this);

    /// <summary>Puces à choix unique : sélectionner l'une désélectionne les autres.</summary>
    public static List<Choice> Group<T>(IEnumerable<(string Label, T Value)> options, T selected, Action<T> onChanged)
    {
        var choices = new List<Choice>();
        foreach (var (label, value) in options)
        {
            choices.Add(new Choice(label, value, picked =>
            {
                foreach (var choice in choices)
                    choice.IsSelected = ReferenceEquals(choice, picked);
                onChanged((T)picked.Value!);
            }, EqualityComparer<T>.Default.Equals(value, selected)));
        }

        return choices;
    }
}
