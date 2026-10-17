using System.Text.RegularExpressions;

namespace AutoNexusMobile.Core.Fleet;

/// <summary>
/// Lit un NIV (VIN) scanné ou saisi. Les étiquettes des montants de porte l'impriment en Code 39,
/// souvent précédé d'un « I » d'import (18 caractères) ; les pare-brise et cartes grises le portent
/// en code-barres ou en QR, parfois noyé dans d'autres champs. On garde les 17 caractères valides.
/// </summary>
public static partial class VinReader
{
    public const int Length = 17;

    [GeneratedRegex("^[A-HJ-NPR-Z0-9]{17}$")]
    private static partial Regex ValidVin();

    // Les lettres I, O et Q sont exclues des NIV : une séquence de 17 caractères permis.
    [GeneratedRegex("[A-HJ-NPR-Z0-9]{17}")]
    private static partial Regex EmbeddedVin();

    public static bool IsValid(string? vin) => vin is not null && ValidVin().IsMatch(vin);

    /// <summary>Normalise une saisie (espaces, tirets, minuscules) sans rien deviner.</summary>
    public static string Normalize(string? raw) =>
        new((raw ?? string.Empty).Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());

    /// <summary>NIV contenu dans un code lu, ou null si le code n'en contient pas.</summary>
    public static string? FromScan(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var compact = Normalize(raw);

        if (IsValid(compact))
            return compact;

        // Préfixe d'import du Code 39 nord-américain : « I » + NIV.
        if (compact.Length == Length + 1 && compact[0] == 'I' && IsValid(compact[1..]))
            return compact[1..];

        // QR ou code à plusieurs champs : le NIV est la séquence de 17 caractères permis délimitée.
        foreach (var token in Regex.Split(raw.ToUpperInvariant(), "[^A-Z0-9]+"))
        {
            if (IsValid(token))
                return token;
        }

        var embedded = EmbeddedVin().Match(compact);
        return embedded.Success && compact.Length <= Length + 2 ? embedded.Value : null;
    }
}
