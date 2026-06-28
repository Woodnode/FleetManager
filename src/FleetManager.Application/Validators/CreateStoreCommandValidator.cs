using FleetManager.Application.Stores.Commands;
using FluentValidation;

namespace FleetManager.Application.Validators;

public class CreateStoreCommandValidator : AbstractValidator<CreateStoreCommand>
{
    public CreateStoreCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Address).NotEmpty().MaximumLength(250);
        RuleFor(x => x.PostalCode).NotEmpty().MaximumLength(10);
        RuleFor(x => x.City).NotEmpty().MaximumLength(100);
    }
}
