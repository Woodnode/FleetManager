using FleetManager.Domain.Enums;
using FleetManager.Domain.Exceptions;
using FleetManager.Domain.ValueObjects;

namespace FleetManager.Domain.Entities;

public class User
{
    public Guid Id { get; private set; }
    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    public Email Email { get; private set; } = null!;
    public string PasswordHash { get; private set; } = string.Empty;
    public UserRole Role { get; private set; }
    public Guid? StoreId { get; private set; }

    private User() { }

    public static User Create(string firstName, string lastName, string email, string passwordHash, UserRole role, Guid? storeId = null)
    {
        if (string.IsNullOrWhiteSpace(firstName))
            throw new DomainException("First name cannot be empty.");

        if (string.IsNullOrWhiteSpace(lastName))
            throw new DomainException("Last name cannot be empty.");

        if (role == UserRole.Technician && storeId is null)
            throw new DomainException("A technician must be assigned to a store.");

        if (role == UserRole.StoreManager && storeId is null)
            throw new DomainException("A store manager must be assigned to a store.");

        return new User
        {
            Id = Guid.NewGuid(),
            FirstName = firstName.Trim(),
            LastName = lastName.Trim(),
            Email = Email.Create(email),
            PasswordHash = passwordHash,
            Role = role,
            StoreId = storeId
        };
    }

    public string FullName => $"{FirstName} {LastName}";

    public void ChangeRole(UserRole newRole, Guid? storeId)
    {
        if ((newRole == UserRole.Technician || newRole == UserRole.StoreManager) && storeId is null)
            throw new DomainException($"A {newRole} must be assigned to a store.");

        Role = newRole;
        StoreId = storeId;
    }
}
