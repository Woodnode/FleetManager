using FleetManager.Application.Interfaces;

namespace FleetManager.Infrastructure.Services;

public class PasswordHasher : IPasswordHasher
{
    public string Hash(string password)
        => BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);

    public bool Verify(string password, string hash)
    {
        // BCrypt throws SaltParseException on malformed/empty hash — treat as mismatch.
        try { return BCrypt.Net.BCrypt.Verify(password, hash); }
        catch { return false; }
    }
}
