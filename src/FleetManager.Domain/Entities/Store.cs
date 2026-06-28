using FleetManager.Domain.Exceptions;

namespace FleetManager.Domain.Entities;

public class Store
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Address { get; private set; } = string.Empty;
    public string PostalCode { get; private set; } = string.Empty;
    public string City { get; private set; } = string.Empty;

    private readonly List<Vehicle> _vehicles = [];
    public IReadOnlyCollection<Vehicle> Vehicles => _vehicles.AsReadOnly();

    private Store() { }

    public static Store Create(string name, string address, string postalCode, string city)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Store name cannot be empty.");

        if (name.Length > 150)
            throw new DomainException("Store name cannot exceed 150 characters.");

        if (string.IsNullOrWhiteSpace(city))
            throw new DomainException("Store city cannot be empty.");

        if (city.Length > 100)
            throw new DomainException("Store city cannot exceed 100 characters.");

        return new Store
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            Address = address.Trim(),
            PostalCode = postalCode.Trim(),
            City = city.Trim()
        };
    }

    public void Update(string name, string address, string postalCode, string city)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Store name cannot be empty.");

        Name = name.Trim();
        Address = address.Trim();
        PostalCode = postalCode.Trim();
        City = city.Trim();
    }
}
