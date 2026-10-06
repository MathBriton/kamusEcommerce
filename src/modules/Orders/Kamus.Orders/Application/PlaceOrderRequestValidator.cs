using FluentValidation;
using Kamus.Orders.Domain;

namespace Kamus.Orders.Application;

internal sealed class PlaceOrderRequestValidator : AbstractValidator<PlaceOrderRequest>
{
    public PlaceOrderRequestValidator()
    {
        RuleFor(r => r.Address).NotNull().SetValidator(new AddressValidator());
        RuleFor(r => r.CardNumber)
            .NotEmpty()
            .Must(Kamus.Payments.Contracts.CardNumberRules.IsValid)
            .WithMessage("Número de cartão inválido.");
        RuleFor(r => r.ExpectedTotal).GreaterThan(0);
    }
}

internal sealed class AddressValidator : AbstractValidator<AddressDto>
{
    public AddressValidator()
    {
        RuleFor(a => a.RecipientName).NotEmpty().MaximumLength(150);
        RuleFor(a => a.PostalCode)
            .NotEmpty()
            .Must(cep => new string([.. cep.Where(char.IsAsciiDigit)]).Length == 8)
            .WithMessage("CEP deve ter 8 dígitos.");
        RuleFor(a => a.Street).NotEmpty().MaximumLength(200);
        RuleFor(a => a.Number).NotEmpty().MaximumLength(20);
        RuleFor(a => a.Complement).MaximumLength(100);
        RuleFor(a => a.District).NotEmpty().MaximumLength(100);
        RuleFor(a => a.City).NotEmpty().MaximumLength(100);
        RuleFor(a => a.State).Must(ShippingTable.IsValidState).WithMessage("UF inválida.");
    }
}
