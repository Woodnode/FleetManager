using FleetManager.Application.Interfaces;
using FleetManager.Domain.Enums;

namespace FleetManager.Application.Services;

public class StoreAuthorizationService : IStoreAuthorizationService
{
    public bool CanAccessStore(UserRole? callerRole, Guid? callerStoreId, Guid resourceStoreId)
    {
        if (callerRole == UserRole.Admin) return true;

        // A missing role claim must never grant access, even if the storeId happens to match.
        if (!callerRole.HasValue) return false;

        return callerStoreId.HasValue && callerStoreId.Value == resourceStoreId;
    }
}
