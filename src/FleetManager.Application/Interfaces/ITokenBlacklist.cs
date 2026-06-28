namespace FleetManager.Application.Interfaces;

public interface ITokenBlacklist
{
    void Revoke(string jti, TimeSpan expiresIn);
    bool IsRevoked(string jti);
}
