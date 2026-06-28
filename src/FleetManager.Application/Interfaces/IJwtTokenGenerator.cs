using FleetManager.Domain.Entities;

namespace FleetManager.Application.Interfaces;

public interface IJwtTokenGenerator
{
    string GenerateToken(User user);
}
