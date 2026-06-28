using FleetManager.Domain.Exceptions;

namespace FleetManager.Domain.ValueObjects;

public sealed class Vin : IEquatable<Vin>
{
    public string Value { get; }

    private Vin(string value) => Value = value;

    public static Vin Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException("VIN cannot be empty.");

        var normalized = value.Trim().ToUpperInvariant();

        if (normalized.Length != 17)
            throw new DomainException("VIN must be exactly 17 characters.");

        if (normalized.Any(c => !char.IsAsciiLetterOrDigit(c) || c is 'I' or 'O' or 'Q'))
            throw new DomainException("VIN must contain only alphanumeric characters (I, O, Q are not permitted).");

        return new Vin(normalized);
    }

    public bool Equals(Vin? other) => other is not null && Value == other.Value;
    public override bool Equals(object? obj) => obj is Vin vin && Equals(vin);
    public override int GetHashCode() => Value.GetHashCode();
    public override string ToString() => Value;
}
