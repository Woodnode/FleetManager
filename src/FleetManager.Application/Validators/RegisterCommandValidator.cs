using FleetManager.Application.Auth.Commands;
using FleetManager.Domain.ValueObjects;
using FluentValidation;
using System.Text;

namespace FleetManager.Application.Validators;

public class RegisterCommandValidator : AbstractValidator<RegisterCommand>
{
    /// <summary>Limite de BCrypt, en octets UTF-8 (un caractère accentué en compte deux).</summary>
    public const int MaxPasswordBytes = 72;

    public RegisterCommandValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);

        RuleFor(x => x.Email)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .Matches(Email.Pattern)
            .WithMessage("Format d'email invalide.");

        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(8).WithMessage("Le mot de passe doit contenir au moins 8 caractères.")
            .Matches("[A-Z]").WithMessage("Le mot de passe doit contenir au moins une majuscule.")
            .Matches("[0-9]").WithMessage("Le mot de passe doit contenir au moins un chiffre.")
            // BCrypt ignore tout ce qui dépasse 72 octets : deux mots de passe au même début seraient acceptés.
            .Must(p => p is null || Encoding.UTF8.GetByteCount(p) <= MaxPasswordBytes)
            .WithMessage($"Le mot de passe ne doit pas dépasser {MaxPasswordBytes} octets.");

        RuleFor(x => x.Role).IsInEnum();
    }
}
