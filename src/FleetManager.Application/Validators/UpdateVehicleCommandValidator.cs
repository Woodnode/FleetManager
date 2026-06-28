using FleetManager.Application.Vehicles.Commands;
using FluentValidation;

namespace FleetManager.Application.Validators;

public class UpdateVehicleCommandValidator : AbstractValidator<UpdateVehicleCommand>
{
    public UpdateVehicleCommandValidator()
    {
        RuleFor(x => x.Brand)
            .NotEmpty().WithMessage("Brand is required.")
            .MaximumLength(100);

        RuleFor(x => x.Model)
            .NotEmpty().WithMessage("Model is required.")
            .MaximumLength(100);

        RuleFor(x => x.Year)
            .InclusiveBetween(1990, DateTime.UtcNow.Year + 1)
            .WithMessage($"Year must be between 1990 and {DateTime.UtcNow.Year + 1}.");

        RuleFor(x => x.Mileage)
            .GreaterThanOrEqualTo(0).WithMessage("Mileage cannot be negative.");

        RuleFor(x => x.StoreId)
            .NotEmpty().WithMessage("StoreId is required.");
    }
}
