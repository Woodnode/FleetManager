using System.Text.RegularExpressions;
using FleetManager.Domain.Exceptions;

namespace FleetManager.Domain.ValueObjects;

public sealed class Email : IEquatable<Email>
{
    // RFC 5321-compatible: rejects local parts or domains with special characters,
    // CRLF injection, semicolons, script tags, etc.
    public const string Pattern = @"^[a-zA-Z0-9._%+\-]+@[a-zA-Z0-9.\-]+\.[a-zA-Z]{2,}$";
    private static readonly Regex ValidEmail = new(Pattern, RegexOptions.Compiled);

    public string Value { get; }

    private Email(string value) => Value = value;

    public static Email Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException("Email cannot be empty.");

        var normalized = value.Trim().ToLowerInvariant();

        if (normalized.Length > 254)
            throw new DomainException("Email cannot exceed 254 characters.");

        if (!ValidEmail.IsMatch(normalized))
            throw new DomainException($"'{value}' is not a valid email address.");

        return new Email(normalized);
    }

    public bool Equals(Email? other) => other is not null && Value == other.Value;
    public override bool Equals(object? obj) => obj is Email email && Equals(email);
    public override int GetHashCode() => Value.GetHashCode();
    public override string ToString() => Value;
}
