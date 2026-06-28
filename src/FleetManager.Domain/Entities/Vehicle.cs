using FleetManager.Domain.Enums;
using FleetManager.Domain.Exceptions;
using FleetManager.Domain.Interfaces;
using FleetManager.Domain.ValueObjects;

namespace FleetManager.Domain.Entities;

public class Vehicle : ISoftDeletable
{
    public Guid Id { get; private set; }
    public Vin Vin { get; private set; } = null!;
    public string Brand { get; private set; } = string.Empty;
    public string Model { get; private set; } = string.Empty;
    public int Year { get; private set; }
    public int Mileage { get; private set; }
    public VehicleStatus Status { get; private set; }
    public Guid StoreId { get; private set; }
    public Store Store { get; private set; } = null!;

    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAt { get; private set; }

    private readonly List<Intervention> _interventions = [];
    public IReadOnlyCollection<Intervention> Interventions => _interventions.AsReadOnly();

    private Vehicle() { }

    public static Vehicle Create(string vin, string brand, string model, int year, int mileage, Guid storeId)
    {
        if (string.IsNullOrWhiteSpace(brand))
            throw new DomainException("Brand cannot be empty.");

        if (brand.Length > 100)
            throw new DomainException("Brand cannot exceed 100 characters.");

        if (string.IsNullOrWhiteSpace(model))
            throw new DomainException("Model cannot be empty.");

        if (model.Length > 100)
            throw new DomainException("Model cannot exceed 100 characters.");

        if (year < 1886 || year > DateTime.UtcNow.Year + 1)
            throw new DomainException($"Year {year} is not valid.");

        if (mileage < 0)
            throw new DomainException("Mileage cannot be negative.");

        return new Vehicle
        {
            Id = Guid.NewGuid(),
            Vin = Vin.Create(vin),
            Brand = brand.Trim(),
            Model = model.Trim(),
            Year = year,
            Mileage = mileage,
            Status = VehicleStatus.Available,
            StoreId = storeId
        };
    }

    public void Update(string brand, string model, int year, int mileage, Guid storeId)
    {
        if (string.IsNullOrWhiteSpace(brand))
            throw new DomainException("Brand cannot be empty.");

        if (brand.Length > 100)
            throw new DomainException("Brand cannot exceed 100 characters.");

        if (string.IsNullOrWhiteSpace(model))
            throw new DomainException("Model cannot be empty.");

        if (model.Length > 100)
            throw new DomainException("Model cannot exceed 100 characters.");

        if (mileage < Mileage)
            throw new DomainException("Mileage cannot decrease.");

        Brand = brand.Trim();
        Model = model.Trim();
        Year = year;
        Mileage = mileage;
        StoreId = storeId;
    }

    public void ChangeStatus(VehicleStatus newStatus)
    {
        if (Status == VehicleStatus.Sold)
            throw new DomainException("A sold vehicle cannot change status.");

        if (Status == newStatus)
            return;

        Status = newStatus;
    }

    public void SoftDelete()
    {
        IsDeleted = true;
        DeletedAt = DateTime.UtcNow;
    }
}
