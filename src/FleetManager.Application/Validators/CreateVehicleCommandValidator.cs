using FleetManager.Application.Vehicles.Commands;
using FluentValidation;
using System.Text.RegularExpressions;

namespace FleetManager.Application.Validators;

public class CreateVehicleCommandValidator : AbstractValidator<CreateVehicleCommand>
{
    public CreateVehicleCommandValidator()
    {
        RuleFor(x => x.Vin)
            .NotEmpty().WithMessage("VIN is required.")
            .Length(17).WithMessage("VIN must be exactly 17 characters.")
            .Matches(@"^[A-HJ-NPR-Z0-9]{17}$", RegexOptions.IgnoreCase)
            .WithMessage("VIN must contain only alphanumeric characters (I, O, Q are not permitted).");

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
