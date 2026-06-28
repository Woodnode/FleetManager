using FleetManager.Application.Vehicles.Commands;
using FluentValidation;

namespace FleetManager.Application.Validators;

public class ChangeVehicleStatusCommandValidator : AbstractValidator<ChangeVehicleStatusCommand>
{
    public ChangeVehicleStatusCommandValidator()
    {
        RuleFor(x => x.NewStatus)
            .IsInEnum()
            .WithMessage("Le statut fourni est invalide.");
    }
}
