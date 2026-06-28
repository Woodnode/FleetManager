using FleetManager.Application.Interventions.Commands;
using FluentValidation;

namespace FleetManager.Application.Validators;

public class CreateInterventionCommandValidator : AbstractValidator<CreateInterventionCommand>
{
    public CreateInterventionCommandValidator()
    {
        RuleFor(x => x.VehicleId).NotEmpty();
        RuleFor(x => x.StoreId).NotEmpty();
        RuleFor(x => x.TechnicianId).NotEmpty();
        RuleFor(x => x.Type).IsInEnum().WithMessage("Type d'intervention invalide.");

        RuleFor(x => x.PlannedStartDate)
            .NotEmpty()
            .GreaterThanOrEqualTo(_ => DateTime.UtcNow.Date)
            .WithMessage("Planned start date cannot be in the past.");

        RuleFor(x => x.PlannedEndDate)
            .NotEmpty()
            .GreaterThan(x => x.PlannedStartDate)
            .WithMessage("Planned end date must be after start date.");
    }
}
