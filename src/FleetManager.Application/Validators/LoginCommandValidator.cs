using FleetManager.Application.Auth.Commands;
using FleetManager.Domain.ValueObjects;
using FluentValidation;

namespace FleetManager.Application.Validators;

public class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Email)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("L'email est requis.")
            .Matches(Email.Pattern)
            .WithMessage("Format d'email invalide.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Le mot de passe est requis.");
    }
}
