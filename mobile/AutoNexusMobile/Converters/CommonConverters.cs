using System.Collections;
using System.Globalization;

namespace AutoNexusMobile.Converters;

public sealed class InvertedBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value is not true;
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => value is not true;
}

/// <summary>Vrai si la valeur est renseignée : texte non vide, objet non nul, nombre non nul, liste non vide.</summary>
public sealed class HasValueConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var has = value switch
        {
            null => false,
            string text => !string.IsNullOrWhiteSpace(text),
            int number => number != 0,
            ICollection collection => collection.Count > 0,
            _ => true,
        };
        return parameter as string == "invert" ? !has : has;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}
