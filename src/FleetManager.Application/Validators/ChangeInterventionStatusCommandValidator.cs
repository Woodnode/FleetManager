using FleetManager.Application.Interventions.Commands;
using FleetManager.Domain.Enums;
using FluentValidation;

namespace FleetManager.Application.Validators;

public class ChangeInterventionStatusCommandValidator : AbstractValidator<ChangeInterventionStatusCommand>
{
    public ChangeInterventionStatusCommandValidator()
    {
        RuleFor(x => x.NewStatus)
            .IsInEnum()
            .WithMessage("Le statut fourni est invalide.");

        RuleFor(x => x.Comment)
            .MaximumLength(1000)
            .WithMessage("Le commentaire ne peut pas dépasser 1000 caractères.");

        RuleFor(x => x.Mileage)
            .InclusiveBetween(0, 2_000_000)
            .When(x => x.Mileage.HasValue)
            .WithMessage("Le kilométrage doit être compris entre 0 et 2 000 000 km.");

        RuleFor(x => x.Mileage)
            .Null()
            .When(x => x.NewStatus != InterventionStatus.Completed)
            .WithMessage("Le kilométrage ne se relève qu'à la clôture d'une intervention.");
    }
}
