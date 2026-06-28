using FleetManager.Application.Interventions.Commands;
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
    }
}
