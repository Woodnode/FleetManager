using FleetManager.Application.Common;
using FleetManager.Application.Interfaces;
using FleetManager.Domain.Enums;
using FleetManager.Domain.Interfaces;
using MediatR;

namespace FleetManager.Application.Interventions.Queries;

/// <summary>
/// Interventions confiées au technicien connecté sur une période (sa journée, vue de son fuseau),
/// plus celles encore ouvertes dont la fin prévue est passée.
/// </summary>
public record GetMyInterventionsQuery(DateTime? FromUtc = null, DateTime? ToUtc = null) : IRequest<Result<IReadOnlyList<InterventionDto>>>;

public class GetMyInterventionsQueryHandler : IRequestHandler<GetMyInterventionsQuery, Result<IReadOnlyList<InterventionDto>>>
{
    // Une semaine suffit au planning du technicien et borne le coût de la requête.
    private static readonly TimeSpan MaxRange = TimeSpan.FromDays(7);

    private readonly IInterventionRepository _interventionRepository;
    private readonly ICurrentUserService _currentUser;

    public GetMyInterventionsQueryHandler(IInterventionRepository interventionRepository, ICurrentUserService currentUser)
    {
        _interventionRepository = interventionRepository;
        _currentUser            = currentUser;
    }

    public async Task<Result<IReadOnlyList<InterventionDto>>> Handle(GetMyInterventionsQuery request, CancellationToken cancellationToken)
    {
        if (_currentUser.Role != UserRole.Technician || !_currentUser.UserId.HasValue || !_currentUser.StoreId.HasValue)
            return Result.Failure<IReadOnlyList<InterventionDto>>(Error.Forbidden("Réservé aux techniciens."));

        var from = request.FromUtc is { } f ? DateTime.SpecifyKind(f.ToUniversalTime(), DateTimeKind.Utc) : DateTime.UtcNow.Date;
        var to   = request.ToUtc   is { } t ? DateTime.SpecifyKind(t.ToUniversalTime(), DateTimeKind.Utc) : from.AddDays(1);

        if (to <= from)
            return Result.Failure<IReadOnlyList<InterventionDto>>(Error.Validation("La fin de la période doit suivre son début."));
        if (to - from > MaxRange)
            return Result.Failure<IReadOnlyList<InterventionDto>>(Error.Validation("La période ne peut pas dépasser 7 jours."));

        var entities = await _interventionRepository.GetTechnicianAgendaAsync(
            _currentUser.UserId.Value, _currentUser.StoreId.Value, from, to, cancellationToken);

        return Result.Success<IReadOnlyList<InterventionDto>>(entities.Select(InterventionDto.FromEntity).ToList().AsReadOnly());
    }
}
